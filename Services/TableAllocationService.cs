using FYP.Data;
using FYP.Models;
using Microsoft.EntityFrameworkCore;

namespace FYP.Services
{
    public class TableAllocationService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<TableAllocationService> _logger;
        private readonly IEmailService _emailService;

        public TableAllocationService(ApplicationDbContext context, ILogger<TableAllocationService> logger, IEmailService emailService)
        {
            _context = context;
            _logger = logger;
            _emailService = emailService;
        }

        /// <summary>
        /// Auto-allocate tables for all pending reservations in order (FIFO)
        /// </summary>
        public async Task<AutoAllocationSummary> AutoAllocatePendingReservationsAsync(string userId)
        {
            var summary = new AutoAllocationSummary();

            // Get all pending reservations ordered by creation time (FIFO)
            var pendingReservations = await _context.Reservations
                .Include(r => r.ReservationTables)
                .Include(r => r.ReservationStatus)
                .Include(r => r.Customer)
                .Include(r => r.Guest)
                .Where(r => r.ReservationStatus.StatusName == "Pending")
                .OrderBy(r => r.CreatedAt) // FIFO order
                .ToListAsync();

            if (!pendingReservations.Any())
            {
                summary.Message = "No pending reservations found.";
                _logger.LogInformation("Auto-allocation: No pending reservations");
                return summary;
            }

            _logger.LogInformation("Auto-allocation: Processing {Count} pending reservations in order", 
                pendingReservations.Count);

            var confirmedStatusId = await _context.ReservationStatuses
                .Where(s => s.StatusName == "Confirmed")
                .Select(s => s.ReservationStatusID)
                .FirstOrDefaultAsync();

            foreach (var reservation in pendingReservations)
            {
                try
                {
                    // Skip if already has tables assigned
                    if (reservation.ReservationTables.Any())
                    {
                        summary.Skipped++;
                        summary.SkippedReservations.Add(new AllocationDetail
                        {
                            ReservationId = reservation.ReservationID,
                            PartySize = reservation.PartySize,
                            Message = "Already has tables assigned"
                        });
                        continue;
                    }

                    // Try to find allocation
                    var allocation = await FindBestAllocationAsync(
                        reservation.RestaurantID,
                        reservation.ReservationDate,
                        reservation.ReservationTime,
                        reservation.Duration,
                        reservation.PartySize
                    );

                    if (allocation.Success)
                    {
                        using var transaction = await _context.Database.BeginTransactionAsync();
                        
                        try
                        {
                            // Link allocated tables
                            foreach (var tableId in allocation.AllocatedTableIds)
                            {
                                var reservationTable = new ReservationTables
                                {
                                    ReservationID = reservation.ReservationID,
                                    TableID = tableId,
                                    CreatedBy = userId,
                                    UpdatedBy = userId,
                                    CreatedAt = DateTime.UtcNow,
                                    UpdatedAt = DateTime.UtcNow
                                };
                                _context.ReservationTables.Add(reservationTable);
                            }

                            // Update status to Confirmed
                            reservation.ReservationStatusID = confirmedStatusId;
                            reservation.UpdatedBy = userId;
                            reservation.UpdatedAt = DateTime.UtcNow;

                            await _context.SaveChangesAsync();

                            // Log the action
                            await LogReservationActionAsync(
                                reservation.ReservationID,
                                "AutoAllocated",
                                allocation.AllocationStrategy,
                                userId
                            );

                            await transaction.CommitAsync();

                            // Send confirmation email
                            try
                            {
                                var customerEmail = reservation.IsGuest ? reservation.Guest?.Email : reservation.Customer?.Email;
                                var customerName = reservation.IsGuest 
                                    ? $"{reservation.Guest?.FirstName} {reservation.Guest?.LastName}" 
                                    : $"{reservation.Customer?.FirstName} {reservation.Customer?.LastName}";

                                if (!string.IsNullOrEmpty(customerEmail))
                                {
                                    await _emailService.SendTableAllocationEmailAsync(
                                        customerEmail,
                                        customerName,
                                        reservation.ReservationDate,
                                        reservation.ReservationTime,
                                        reservation.PartySize,
                                        allocation.AllocationStrategy
                                    );
                                }
                            }
                            catch (Exception emailEx)
                            {
                                _logger.LogWarning(emailEx, "Failed to send allocation email for reservation {ReservationId}", reservation.ReservationID);
                                // Don't fail the allocation if email fails
                            }

                            summary.Allocated++;
                            summary.AllocatedReservations.Add(new AllocationDetail
                            {
                                ReservationId = reservation.ReservationID,
                                PartySize = reservation.PartySize,
                                TableIds = allocation.AllocatedTableIds,
                                Message = allocation.AllocationStrategy
                            });

                            _logger.LogInformation(
                                "Auto-allocated reservation {ReservationId}: {Strategy}",
                                reservation.ReservationID,
                                allocation.AllocationStrategy
                            );
                        }
                        catch (Exception ex)
                        {
                            await transaction.RollbackAsync();
                            throw;
                        }
                    }
                    else
                    {
                        summary.Failed++;
                        summary.FailedReservations.Add(new AllocationDetail
                        {
                            ReservationId = reservation.ReservationID,
                            PartySize = reservation.PartySize,
                            Message = allocation.ErrorMessage
                        });

                        _logger.LogWarning(
                            "Failed to auto-allocate reservation {ReservationId}: {Error}",
                            reservation.ReservationID,
                            allocation.ErrorMessage
                        );
                    }
                }
                catch (Exception ex)
                {
                    summary.Failed++;
                    summary.FailedReservations.Add(new AllocationDetail
                    {
                        ReservationId = reservation.ReservationID,
                        PartySize = reservation.PartySize,
                        Message = $"Error: {ex.Message}"
                    });

                    _logger.LogError(ex, 
                        "Exception during auto-allocation for reservation {ReservationId}",
                        reservation.ReservationID
                    );
                }
            }

            summary.Message = $"Processed {pendingReservations.Count} reservations: {summary.Allocated} allocated, {summary.Failed} failed, {summary.Skipped} skipped.";
            _logger.LogInformation("Auto-allocation completed: {Summary}", summary.Message);

            return summary;
        }

        /// <summary>
        /// Find the best table allocation for a reservation using intelligent matching algorithms
        /// </summary>
        public async Task<AllocationResult> FindBestAllocationAsync(
            int restaurantId,
            DateTime reservationDate,
            TimeSpan reservationTime,
            int durationMinutes,
            int partySize)
        {
            var result = new AllocationResult();

            // Validation
            if (partySize <= 0)
            {
                result.Success = false;
                result.ErrorMessage = "Party size must be at least 1.";
                return result;
            }

            if (durationMinutes <= 0)
            {
                result.Success = false;
                result.ErrorMessage = "Duration must be positive.";
                return result;
            }

            // Build time window
            var startUtc = new DateTime(
                reservationDate.Year, reservationDate.Month, reservationDate.Day,
                reservationTime.Hours, reservationTime.Minutes, 0, DateTimeKind.Utc);
            var endUtc = startUtc.AddMinutes(durationMinutes);

            _logger.LogInformation(
                "Finding allocation for party of {PartySize} on {Date} at {Time} for {Duration} minutes",
                partySize, reservationDate, reservationTime, durationMinutes);

            // Get all tables for the restaurant that are available (not blocked/cleaning)
            var allTables = await _context.Tables
                .Where(t => t.RestaurantID == restaurantId && t.IsAvailable)
                .OrderBy(t => t.Capacity)
                .ToListAsync();

            if (!allTables.Any())
            {
                result.Success = false;
                result.ErrorMessage = "No tables available at this restaurant.";
                _logger.LogWarning("No tables found for restaurant {RestaurantId}", restaurantId);
                return result;
            }

            // Get all existing reservations that might overlap with our time window
            var existingReservations = await _context.Reservations
                .Include(r => r.ReservationTables)
                .Where(r => r.ReservationDate == reservationDate.Date)
                .Select(r => new
                {
                    r.ReservationID,
                    r.ReservationDate,
                    r.ReservationTime,
                    r.Duration,
                    TableIds = r.ReservationTables.Select(rt => rt.TableID).ToList()
                })
                .ToListAsync();

            _logger.LogInformation("Found {Count} existing reservations on {Date}", 
                existingReservations.Count, reservationDate.Date);

            // Determine which tables are available (no time overlap)
            var availableTables = new List<Table>();
            foreach (var table in allTables)
            {
                bool hasOverlap = false;
                
                foreach (var existing in existingReservations)
                {
                    if (existing.TableIds.Contains(table.TableID))
                    {
                        var existingStart = new DateTime(
                            existing.ReservationDate.Year,
                            existing.ReservationDate.Month,
                            existing.ReservationDate.Day,
                            existing.ReservationTime.Hours,
                            existing.ReservationTime.Minutes, 0, DateTimeKind.Utc);
                        var existingEnd = existingStart.AddMinutes(existing.Duration);

                        // Check for time overlap: [start1, end1) overlaps [start2, end2) if start1 < end2 AND start2 < end1
                        if (startUtc < existingEnd && existingStart < endUtc)
                        {
                            hasOverlap = true;
                            _logger.LogDebug(
                                "Table {TableId} conflicts with reservation {ReservationId}",
                                table.TableID, existing.ReservationID);
                            break;
                        }
                    }
                }

                if (!hasOverlap)
                {
                    availableTables.Add(table);
                }
            }

            if (!availableTables.Any())
            {
                result.Success = false;
                result.ErrorMessage = "No tables are available for the selected time slot.";
                _logger.LogWarning("No available tables for time slot {Start} - {End}", startUtc, endUtc);
                return result;
            }

            _logger.LogInformation("Found {Count} available tables", availableTables.Count);

            // ========== PRIORITY RULE 1: Exact capacity match (single table) =========
            var exactFitTable = availableTables
                .FirstOrDefault(t => t.Capacity == partySize);

            if (exactFitTable != null)
            {
                result.Success = true;
                result.AllocatedTableIds = new List<int> { exactFitTable.TableID };
                result.TotalCapacity = exactFitTable.Capacity;
                result.WastedSeats = 0;
                result.AllocationStrategy = $"Exact fit: Table {exactFitTable.TableNumber} (capacity {exactFitTable.Capacity})";
                _logger.LogInformation("Found exact fit: Table {TableId}", exactFitTable.TableID);
                return result;
            }

            // ========== PRIORITY RULE 2: Single table with closest larger capacity =========
            var singleTableFit = availableTables
                .Where(t => t.Capacity >= partySize)
                .OrderBy(t => t.Capacity) // Smallest table that fits
                .FirstOrDefault();

            if (singleTableFit != null)
            {
                result.Success = true;
                result.AllocatedTableIds = new List<int> { singleTableFit.TableID };
                result.TotalCapacity = singleTableFit.Capacity;
                result.WastedSeats = singleTableFit.Capacity - partySize;
                result.AllocationStrategy = $"Single table: Table {singleTableFit.TableNumber} (capacity {singleTableFit.Capacity}, {result.WastedSeats} seats unused)";
                _logger.LogInformation("Found single table fit: Table {TableId} with {Wasted} wasted seats", 
                    singleTableFit.TableID, result.WastedSeats);
                return result;
            }

            // ========== PRIORITY RULE 3: Combine joinable tables =========
            var joinableTables = availableTables.Where(t => t.IsJoinable).ToList();

            if (joinableTables.Any())
            {
                _logger.LogInformation("Attempting to find joinable combination from {Count} joinable tables", 
                    joinableTables.Count);

                // Get all configured table joins
                var tableJoins = await _context.TablesJoins
                    .Where(tj => joinableTables.Select(t => t.TableID).Contains(tj.PrimaryTableID) ||
                                 joinableTables.Select(t => t.TableID).Contains(tj.JoinedTableID))
                    .ToListAsync();

                _logger.LogInformation("Found {Count} configured joins", tableJoins.Count);

                // Try to find best combination
                var bestCombination = FindBestJoinCombination(joinableTables, tableJoins, partySize);

                if (bestCombination != null && bestCombination.Any())
                {
                    result.Success = true;
                    result.AllocatedTableIds = bestCombination.Select(t => t.TableID).ToList();
                    result.TotalCapacity = bestCombination.Sum(t => t.Capacity);
                    result.WastedSeats = result.TotalCapacity - partySize;
                    
                    var tableNumbers = string.Join(", ", bestCombination.Select(t => t.TableNumber));
                    result.AllocationStrategy = $"Combined {bestCombination.Count} tables: {tableNumbers} (total capacity {result.TotalCapacity}, {result.WastedSeats} seats unused)";
                    
                    _logger.LogInformation("Found joinable combination: {TableIds} with {Wasted} wasted seats",
                        string.Join(", ", result.AllocatedTableIds), result.WastedSeats);
                    return result;
                }
            }

            // ========== No suitable allocation found =========
            result.Success = false;
            result.ErrorMessage = $"Cannot accommodate party of {partySize}. Largest available table capacity is {availableTables.Max(t => t.Capacity)}. Consider joining tables or choosing a different time.";
            _logger.LogWarning("No suitable allocation found for party of {PartySize}", partySize);
            return result;
        }

        /// <summary>
        /// Find best combination of joinable tables using graph-based algorithms
        /// Implements PRIORITY RULE 4: Smallest number of tables with least unused seats
        /// </summary>
        private List<Table>? FindBestJoinCombination(
            List<Table> joinableTables,
            List<TablesJoin> configuredJoins,
            int partySize)
        {
            // Build adjacency graph of which tables can join
            var adjacency = new Dictionary<int, HashSet<int>>();
            foreach (var table in joinableTables)
            {
                adjacency[table.TableID] = new HashSet<int>();
            }

            // If we have configured joins, use them (respects join rules)
            if (configuredJoins.Any())
            {
                foreach (var join in configuredJoins)
                {
                    if (adjacency.ContainsKey(join.PrimaryTableID) && adjacency.ContainsKey(join.JoinedTableID))
                    {
                        adjacency[join.PrimaryTableID].Add(join.JoinedTableID);
                        adjacency[join.JoinedTableID].Add(join.PrimaryTableID); // Bidirectional
                    }
                }
            }
            else
            {
                // If no configured joins exist, allow any joinable table to join with any other
                // This is a fallback behavior
                _logger.LogWarning("No configured joins found. Using permissive join logic.");
                foreach (var table1 in joinableTables)
                {
                    foreach (var table2 in joinableTables)
                    {
                        if (table1.TableID != table2.TableID)
                        {
                            adjacency[table1.TableID].Add(table2.TableID);
                        }
                    }
                }
            }

            List<Table>? bestCombination = null;
            int bestWaste = int.MaxValue;

            // ========== Try pairs first (most common, least waste) =========
            for (int i = 0; i < joinableTables.Count; i++)
            {
                for (int j = i + 1; j < joinableTables.Count; j++)
                {
                    var table1 = joinableTables[i];
                    var table2 = joinableTables[j];

                    // Check if these two tables can join
                    if (adjacency[table1.TableID].Contains(table2.TableID))
                    {
                        var totalCapacity = table1.Capacity + table2.Capacity;
                        if (totalCapacity >= partySize)
                        {
                            var waste = totalCapacity - partySize;
                            if (waste < bestWaste)
                            {
                                bestWaste = waste;
                                bestCombination = new List<Table> { table1, table2 };
                                
                                // If exact fit, return immediately
                                if (waste == 0)
                                {
                                    _logger.LogInformation("Found exact fit with 2 tables: {T1}, {T2}", 
                                        table1.TableID, table2.TableID);
                                    return bestCombination;
                                }
                            }
                        }
                    }
                }
            }

            // If we found a good pair, return it
            if (bestCombination != null)
            {
                _logger.LogInformation("Best pair found with {Waste} wasted seats", bestWaste);
                return bestCombination;
            }

            // ========== Try triplets (3 tables) ==========
            for (int i = 0; i < joinableTables.Count; i++)
            {
                for (int j = i + 1; j < joinableTables.Count; j++)
                {
                    for (int k = j + 1; k < joinableTables.Count; k++)
                    {
                        var table1 = joinableTables[i];
                        var table2 = joinableTables[j];
                        var table3 = joinableTables[k];

                        // Check if all three form a connected group
                        // A valid triplet requires connectivity: each table connects to at least one other
                        bool canJoin = IsConnectedGroup(new[] { table1.TableID, table2.TableID, table3.TableID }, adjacency);

                        if (canJoin)
                        {
                            var totalCapacity = table1.Capacity + table2.Capacity + table3.Capacity;
                            if (totalCapacity >= partySize)
                            {
                                var waste = totalCapacity - partySize;
                                if (waste < bestWaste)
                                {
                                    bestWaste = waste;
                                    bestCombination = new List<Table> { table1, table2, table3 };
                                    
                                    // If exact fit, return immediately
                                    if (waste == 0)
                                    {
                                        _logger.LogInformation("Found exact fit with 3 tables");
                                        return bestCombination;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // If we found a triplet, return it
            if (bestCombination != null)
            {
                _logger.LogInformation("Best triplet found with {Waste} wasted seats", bestWaste);
                return bestCombination;
            }

            // ========== Try larger combinations (4+ tables) if needed ==========
            // For performance reasons, limit to 4 tables max
            var largerCombination = FindLargerCombination(joinableTables, adjacency, partySize, maxTables: 4);
            if (largerCombination != null)
            {
                _logger.LogInformation("Found larger combination with {Count} tables", largerCombination.Count);
                return largerCombination;
            }

            _logger.LogWarning("No valid joinable combination found for party of {PartySize}", partySize);
            return null;
        }

        /// <summary>
        /// Check if a group of tables forms a connected graph (all tables can physically join)
        /// </summary>
        private bool IsConnectedGroup(int[] tableIds, Dictionary<int, HashSet<int>> adjacency)
        {
            if (tableIds.Length <= 1) return true;

            // Use BFS to check connectivity
            var visited = new HashSet<int>();
            var queue = new Queue<int>();
            queue.Enqueue(tableIds[0]);
            visited.Add(tableIds[0]);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                
                foreach (var neighbor in adjacency[current])
                {
                    if (tableIds.Contains(neighbor) && !visited.Contains(neighbor))
                    {
                        visited.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }
            }

            // All tables in the group must be reachable
            return visited.Count == tableIds.Length;
        }

        /// <summary>
        /// Find larger combinations (4+ tables) using recursive approach
        /// </summary>
        private List<Table>? FindLargerCombination(
            List<Table> joinableTables,
            Dictionary<int, HashSet<int>> adjacency,
            int partySize,
            int maxTables)
        {
            // Try 4-table combinations
            if (joinableTables.Count >= 4)
            {
                for (int i = 0; i < joinableTables.Count; i++)
                {
                    for (int j = i + 1; j < joinableTables.Count; j++)
                    {
                        for (int k = j + 1; k < joinableTables.Count; k++)
                        {
                            for (int l = k + 1; l < joinableTables.Count; l++)
                            {
                                var combo = new[] { joinableTables[i], joinableTables[j], joinableTables[k], joinableTables[l] };
                                var tableIds = combo.Select(t => t.TableID).ToArray();

                                if (IsConnectedGroup(tableIds, adjacency))
                                {
                                    var totalCapacity = combo.Sum(t => t.Capacity);
                                    if (totalCapacity >= partySize)
                                    {
                                        return combo.ToList();
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Create reservation with allocated tables (transactional)
        /// </summary>
        public async Task<Reservation> CreateReservationAsync(
            AllocationResult allocation,
            int? customerId,
            int? guestId,
            bool isGuest,
            int restaurantId,
            DateTime reservationDate,
            TimeSpan reservationTime,
            int duration,
            int partySize,
            string? notes,
            int statusId,
            bool isWalkIn,
            string userId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Create reservation
                var reservation = new Reservation
                {
                    CustomerID = customerId,
                    GuestID = guestId,
                    IsGuest = isGuest,
                    RestaurantID = restaurantId,
                    ReservationDate = reservationDate.Date,
                    ReservationTime = reservationTime,
                    Duration = duration,
                    PartySize = partySize,
                    Notes = notes,
                    ReservationStatusID = statusId,
                    ReservationType = !isWalkIn,
                    CreatedBy = userId,
                    UpdatedBy = userId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Reservations.Add(reservation);
                await _context.SaveChangesAsync();

                // Link allocated tables
                foreach (var tableId in allocation.AllocatedTableIds)
                {
                    var reservationTable = new ReservationTables
                    {
                        ReservationID = reservation.ReservationID,
                        TableID = tableId,
                        CreatedBy = userId,
                        UpdatedBy = userId,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.ReservationTables.Add(reservationTable);
                }

                await _context.SaveChangesAsync();

                // Log the action
                await LogReservationActionAsync(
                    reservation.ReservationID,
                    "Created",
                    allocation.AllocationStrategy,
                    userId);

                await transaction.CommitAsync();

                // Send confirmation email
                try
                {
                    string customerEmail = null;
                    string customerName = null;

                    if (isGuest && guestId.HasValue)
                    {
                        var guest = await _context.Guests.FindAsync(guestId.Value);
                        customerEmail = guest?.Email;
                        customerName = $"{guest?.FirstName} {guest?.LastName}";
                    }
                    else if (customerId.HasValue)
                    {
                        var customer = await _context.Customers.FindAsync(customerId.Value);
                        customerEmail = customer?.Email;
                        customerName = $"{customer?.FirstName} {customer?.LastName}";
                    }

                    if (!string.IsNullOrEmpty(customerEmail))
                    {
                        await _emailService.SendTableAllocationEmailAsync(
                            customerEmail,
                            customerName,
                            reservationDate,
                            reservationTime,
                            partySize,
                            allocation.AllocationStrategy
                        );
                    }
                }
                catch (Exception emailEx)
                {
                    _logger.LogWarning(emailEx, "Failed to send allocation email for reservation {ReservationId}", reservation.ReservationID);
                    // Don't fail the reservation if email fails
                }

                _logger.LogInformation(
                    "Reservation {ReservationId} created successfully. Strategy: {Strategy}, Tables: {Tables}",
                    reservation.ReservationID,
                    allocation.AllocationStrategy,
                    string.Join(", ", allocation.AllocatedTableIds));

                return reservation;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to create reservation with allocation");
                throw;
            }
        }

        /// <summary>
        /// Log reservation action to audit trail
        /// </summary>
        private async Task LogReservationActionAsync(int reservationId, string actionName, string? details, string userId)
        {
            var actionType = await _context.ActionTypes.FirstOrDefaultAsync(a => a.ActionTypeName == actionName);
            if (actionType == null)
            {
                actionType = new ActionType
                {
                    ActionTypeName = actionName,
                    Description = $"Reservation {actionName}",
                    CreatedBy = "system",
                    UpdatedBy = "system",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.ActionTypes.Add(actionType);
                await _context.SaveChangesAsync();
            }

            var log = new ReservationLog
            {
                ReservationID = reservationId,
                ActionTypeID = actionType.ActionTypeID,
                OldValue = details,
                CreatedBy = userId,
                UpdatedBy = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.ReservationLogs.Add(log);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Override table assignment for an existing reservation (manager function)
        /// </summary>
        public async Task<bool> OverrideTableAssignmentAsync(
            int reservationId,
            List<int> newTableIds,
            string userId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var reservation = await _context.Reservations
                    .Include(r => r.ReservationTables)
                    .FirstOrDefaultAsync(r => r.ReservationID == reservationId);

                if (reservation == null)
                {
                    _logger.LogWarning("Reservation {ReservationId} not found for override", reservationId);
                    return false;
                }

                // Validate new tables exist and are available
                var newTables = await _context.Tables
                    .Where(t => newTableIds.Contains(t.TableID) && t.IsAvailable)
                    .ToListAsync();

                if (newTables.Count != newTableIds.Count)
                {
                    _logger.LogWarning("Some tables in override are not available");
                    return false;
                }

                // Check capacity
                var totalCapacity = newTables.Sum(t => t.Capacity);
                if (totalCapacity < reservation.PartySize)
                {
                    _logger.LogWarning("Override tables insufficient capacity: {Capacity} < {PartySize}",
                        totalCapacity, reservation.PartySize);
                    return false;
                }

                // Remove old assignments
                _context.ReservationTables.RemoveRange(reservation.ReservationTables);

                // Add new assignments
                foreach (var tableId in newTableIds)
                {
                    var reservationTable = new ReservationTables
                    {
                        ReservationID = reservationId,
                        TableID = tableId,
                        CreatedBy = userId,
                        UpdatedBy = userId,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.ReservationTables.Add(reservationTable);
                }

                reservation.UpdatedBy = userId;
                reservation.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                // Log the override
                var oldTableIds = string.Join(", ", reservation.ReservationTables.Select(rt => rt.TableID));
                var newTableIdsStr = string.Join(", ", newTableIds);
                await LogReservationActionAsync(
                    reservationId,
                    "TableOverride",
                    $"Changed from [{oldTableIds}] to [{newTableIdsStr}]",
                    userId);

                await transaction.CommitAsync();

                _logger.LogInformation("Table assignment overridden for reservation {ReservationId} by {UserId}",
                    reservationId, userId);

                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to override table assignment for reservation {ReservationId}", reservationId);
                return false;
            }
        }
    }

    /// <summary>
    /// Result of table allocation attempt
    /// </summary>
    public class AllocationResult
    {
        public bool Success { get; set; }
        public List<int> AllocatedTableIds { get; set; } = new List<int>();
        public int TotalCapacity { get; set; }
        public int WastedSeats { get; set; }
        public string? AllocationStrategy { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Summary of auto-allocation process
    /// </summary>
    public class AutoAllocationSummary
    {
        public int Allocated { get; set; }
        public int Failed { get; set; }
        public int Skipped { get; set; }
        public string Message { get; set; } = "";
        public List<AllocationDetail> AllocatedReservations { get; set; } = new List<AllocationDetail>();
        public List<AllocationDetail> FailedReservations { get; set; } = new List<AllocationDetail>();
        public List<AllocationDetail> SkippedReservations { get; set; } = new List<AllocationDetail>();
    }

    /// <summary>
    /// Detail of individual allocation
    /// </summary>
    public class AllocationDetail
    {
        public int ReservationId { get; set; }
        public int PartySize { get; set; }
        public List<int> TableIds { get; set; } = new List<int>();
        public string Message { get; set; } = "";
    }
}

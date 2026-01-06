# Table Auto-Assign Logic Documentation

## Overview
The Table Auto-Assign system automatically selects the optimal table(s) for customer reservations based on party size, availability, and configured join rules.

---

## ?? Core Requirements Implemented

### 1. Availability Check
- **Time Window Validation**: Checks for overlapping reservations
  - Reservation blocks: `[reservation_time, reservation_time + duration)`
  - Overlap detection: `start1 < end2 AND start2 < end1`
- **Table Status**: Only considers tables where `IsAvailable = true`
- **Edge Cases**:
  - Temporarily blocked/cleaning tables (filtered out)
  - Same-day multiple reservations handled correctly
  - UTC time normalization for consistency

### 2. Capacity Matching Algorithm

#### Priority Rules (in order):

**PRIORITY 1: Exact Match (Single Table)**
```
IF exists table WHERE capacity == partySize
    RETURN that table
    WastedSeats = 0
```
- Example: Party of 4 ? Table with capacity 4 ?

**PRIORITY 2: Minimal Waste (Single Table)**
```
FROM available tables WHERE capacity >= partySize
ORDER BY capacity ASC
RETURN first table
WastedSeats = capacity - partySize
```
- Example: Party of 5 ? Table with capacity 6 (1 wasted seat) ?

**PRIORITY 3: Joinable Combinations (2-4 Tables)**
```
FOREACH combination of joinable tables:
    IF total_capacity >= partySize AND IsConnectedGraph:
        TRACK waste
        PREFER smallest number of tables
        PREFER least waste
```
- Example: Party of 10 ? Tables (6 + 4) instead of (8 + 4) ?
- Validates connectivity using BFS graph traversal

**PRIORITY 4: No Solution**
```
RETURN AllocationResult {
    Success = false,
    ErrorMessage = detailed explanation
}
```

### 3. Join Rules & Validation

#### Graph-Based Join System
```
adjacency[TableID] = HashSet<TableID>
```

**Prioritized Join Strategy (Two-Pass Logic):**

The system attempts to find a solution in two passes, prioritizing your preferences but never blocking a valid physical combination.

**Pass 1: Preferred Joins (Strict)**
- Attempts to form a combination using **ONLY** the join rules configured in `TablesJoins`.
- This ensures optimal layouts (e.g., joining adjacent booths) are prioritized.
- If a valid combination is found, it is returned immediately.

**Pass 2: Permissive Joins (Fallback)**
- If Pass 1 fails (e.g., preferred tables are occupied or capacity isn't met), the system attempts to form a combination using **ANY** tables marked `IsJoinable`.
- In this pass, any joinable table can theoretically join with any other joinable table.
- This ensures that if space exists, the reservation is accepted, even if the layout isn't "standard".

#### Connectivity Check (BFS Algorithm)
```csharp
IsConnectedGroup(tableIds[], adjacency):
    visited = BFS starting from first table
    RETURN visited.Count == tableIds.Length
```
- Ensures all tables in combination are physically adjacent
- Prevents invalid combinations (e.g., Table 1, 3, 5 when only 1-2-3 connect)

### 4. Combination Search Strategy

#### Pairs (Most Efficient)
```
FOR i = 0 to N-1:
    FOR j = i+1 to N-1:
        IF canJoin(i, j) AND capacity >= partySize:
            TRACK if best
```

#### Triplets
```
FOR i, j, k (i < j < k):
    IF IsConnectedGraph(i, j, k) AND capacity >= partySize:
        TRACK if best
```

#### Larger Groups (4+)
- Limited to max 4 tables for performance
- Exponential complexity managed

### 5. Output Structure

```csharp
public class AllocationResult
{
    public bool Success { get; set; }              // True if allocated
    public List<int> AllocatedTableIds { get; set; } // [1, 2] or [5]
    public int TotalCapacity { get; set; }         // Sum of capacities
    public int WastedSeats { get; set; }           // Unused seats
    public string AllocationStrategy { get; set; } // Human-readable explanation
    public string ErrorMessage { get; set; }       // If failed
}
```

**Example Outputs:**
```json
{
    "Success": true,
    "AllocatedTableIds": [4],
    "TotalCapacity": 6,
    "WastedSeats": 0,
    "AllocationStrategy": "Exact fit: Table 4 (capacity 6)"
}
```

```json
{
    "Success": true,
    "AllocatedTableIds": [1, 2],
    "TotalCapacity": 8,
    "WastedSeats": 1,
    "AllocationStrategy": "Combined 2 tables: 1, 2 (total capacity 8, 1 seat unused)"
}
```

```json
{
    "Success": false,
    "ErrorMessage": "Cannot accommodate party of 15. Largest available table capacity is 8. Consider joining tables or choosing a different time."
}
```

---

## ?? Manager Override Functionality

### Manual Table Assignment
```csharp
await _allocationService.OverrideTableAssignmentAsync(
    reservationId: 123,
    newTableIds: [5, 6],
    userId: "manager@example.com"
);
```

**Validations:**
- Tables exist and are available
- Combined capacity = party size
- Logs override in audit trail
- Transaction safety (rollback on failure)

---

## ?? Edge Cases Handled

| Scenario | Handling |
|----------|----------|
| **No tables available at restaurant** | Error: "No tables available at this restaurant" |
| **All tables reserved** | Error: "No tables are available for the selected time slot" |
| **Party size too large** | Tries combinations, then error with max capacity info |
| **Invalid party size (<= 0)** | Error: "Party size must be at least 1" |
| **Invalid duration (<= 0)** | Error: "Duration must be positive" |
| **Overlapping reservations** | Correctly excluded from available pool |
| **Disconnected joinable tables** | Rejected via connectivity check |
| **Concurrent reservation attempts** | Transaction isolation protects against race conditions |
| **Tables marked unavailable (cleaning)** | Filtered out at query level |

---

## ?? Performance Optimizations

1. **Database Queries:**
   - Single query for all tables (`OrderBy(Capacity)`)
   - Single query for date's reservations
   - Indexed joins for efficiency

2. **Algorithm Complexity:**
   - Pairs: O(n�)
   - Triplets: O(n�)
   - Limited to 4 tables max ? O(n4) worst case
   - Early termination on exact fit

3. **Caching Opportunities:**
   - Table adjacency graph (can be cached)
   - Available tables per time slot (can be cached)

---

## ?? Logging & Audit Trail

### Informational Logs
```
Finding allocation for party of 6 on 2025-11-20 at 19:00 for 90 minutes
Found 8 available tables
Found exact fit: Table 8
Reservation 456 created successfully. Strategy: Exact fit, Tables: 8
```

### Warning Logs
```
No configured joins found. Using permissive join logic.
No suitable allocation found for party of 20
```

### Error Logs
```
Failed to create reservation with allocation: [exception details]
```

### Audit Trail (ReservationLogs)
- Action: "Created"
- Details: "Exact fit: Table 4 (capacity 6)"
- User: "customer@example.com"
- Timestamp: UTC

### Override Audit
- Action: "TableOverride"
- Details: "Changed from [3] to [5, 6]"
- User: "manager@example.com"

---

## ?? Testing Scenarios

### Unit Test Cases
1. **Exact Fit:**
   - Party of 4 ? Selects table 4 (capacity 4)
2. **Minimal Waste:**
   - Party of 5 ? Selects table 6 (capacity 6, not 8)
3. **Two Tables:**
   - Party of 8 ? Joins tables (4+4) over (6+6)
4. **Three Tables:**
   - Party of 12 ? Joins (4+4+4)
5. **No Solution:**
   - Party of 50 ? Returns error
6. **Time Overlap:**
   - Existing: 18:00-20:00
   - New: 19:00-21:00
   - Conflict detected ?
7. **No Overlap:**
   - Existing: 18:00-20:00
   - New: 20:30-22:00
   - No conflict ?
8. **Join Validation:**
   - Tables 1,2,3 can join (connected)
   - Tables 1,5 cannot join (disconnected)

---

## ?? Transaction Safety

All reservation creation is wrapped in database transactions:
```csharp
using var transaction = await _context.Database.BeginTransactionAsync();
try {
    // Create reservation
    // Link tables
    // Log action
    await transaction.CommitAsync();
} catch {
    await transaction.RollbackAsync();
    throw;
}
```

**Benefits:**
- Atomic operations (all or nothing)
- No orphaned data
- Protection against concurrent modifications

---

## ?? Usage Examples

### Example 1: Customer Booking
```csharp
var allocation = await _allocationService.FindBestAllocationAsync(
    restaurantId: 1,
    reservationDate: new DateTime(2025, 11, 20),
    reservationTime: new TimeSpan(19, 0, 0),
    durationMinutes: 90,
    partySize: 6
);

if (allocation.Success)
{
    var reservation = await _allocationService.CreateReservationAsync(
        allocation,
        customerId: 42,
        guestId: null,
        isGuest: false,
        restaurantId: 1,
        reservationDate: new DateTime(2025, 11, 20),
        reservationTime: new TimeSpan(19, 0, 0),
        duration: 90,
        partySize: 6,
        notes: "Birthday celebration",
        statusId: 2, // Confirmed
        isWalkIn: false,
        userId: "customer@example.com"
    );
}
```

### Example 2: Employee Walk-In
```csharp
var allocation = await _allocationService.FindBestAllocationAsync(
    restaurantId: 1,
    reservationDate: DateTime.UtcNow.Date,
    reservationTime: new TimeSpan(DateTime.UtcNow.Hour, DateTime.UtcNow.Minute, 0),
    durationMinutes: 60,
    partySize: 4
);

if (allocation.Success)
{
    var walkInCustomerId = await WalkInCustomerSeeder.EnsureWalkInCustomerAsync(services);
    
    var reservation = await _allocationService.CreateReservationAsync(
        allocation,
        customerId: walkInCustomerId,
        guestId: null,
        isGuest: false,
        restaurantId: 1,
        reservationDate: DateTime.UtcNow.Date,
        reservationTime: new TimeSpan(DateTime.UtcNow.Hour, DateTime.UtcNow.Minute, 0),
        duration: 60,
        partySize: 4,
        notes: null,
        statusId: 2,
        isWalkIn: true,
        userId: "employee@example.com"
    );
}
```

### Example 3: Manager Override
```csharp
// Customer originally assigned to Table 3
// Manager reassigns to Tables 5 + 6 for better layout

bool success = await _allocationService.OverrideTableAssignmentAsync(
    reservationId: 789,
    newTableIds: new List<int> { 5, 6 },
    userId: "manager@example.com"
);
```

---

## ?? Related Database Tables

### Tables
```sql
TableID, TableNumber, Capacity, IsJoinable, IsAvailable, RestaurantID
```

### TablesJoins
```sql
TablesJoinID, PrimaryTableID, JoinedTableID, TotalCapacity
```

### Reservations
```sql
ReservationID, ReservationDate, ReservationTime, Duration, PartySize, ...
```

### ReservationTables
```sql
ReservationID, TableID (many-to-many)
```

### ReservationLogs
```sql
LogID, ReservationID, ActionTypeID, OldValue, CreatedBy, CreatedAt
```

---

## ?? Future Enhancements

1. **Machine Learning:**
   - Predict busy times
   - Suggest optimal table configurations

2. **Real-Time Updates:**
   - SignalR for live table availability
   - Push notifications for staff

3. **Advanced Constraints:**
   - VIP sections
   - Window/patio preferences
   - Accessibility requirements

4. **Optimization:**
   - Table turn-over predictions
   - Dynamic duration adjustments
   - Load balancing across shifts

---

## ?? Support

For questions or issues with the table allocation system:
- Check logs in `/Logs` directory
- Review `ReservationLogs` table for audit trail
- Contact system administrator

---

**Version:** 1.0  
**Last Updated:** November 2025  
**Author:** FYP Development Team

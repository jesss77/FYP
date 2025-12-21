using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Collections.Generic;
using System.IO;
using FYP.ViewModels.Reports;

namespace FYP.Services.Pdf
{
    public class ReportPdfGenerator
    {
        public byte[] GeneratePdf(ReportPdfDataVM data)
        {
            var doc = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(20);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(12));

                    page.Header().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text(data.Title).Bold().FontSize(20);
                            if (!string.IsNullOrEmpty(data.SubTitle)) col.Item().Text(data.SubTitle).FontSize(12).FontColor(Colors.Grey.Darken1);
                            col.Item().Text($"Generated: {data.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC").FontSize(9).FontColor(Colors.Grey.Lighten1);
                            if (!string.IsNullOrEmpty(data.GeneratedBy)) col.Item().Text($"By: {data.GeneratedBy}").FontSize(9).FontColor(Colors.Grey.Lighten1);
                            if (!string.IsNullOrEmpty(data.FromDate) || !string.IsNullOrEmpty(data.ToDate)) col.Item().Text($"Range: {data.FromDate ?? "-"} - {data.ToDate ?? "-"}").FontSize(9).FontColor(Colors.Grey.Lighten1);
                        });
                    });

                    page.Content().PaddingVertical(10).Column(col =>
                    {
                        // KPIs
                        if (data.KPIs != null && data.KPIs.Count > 0)
                        {
                            col.Item().Row(r =>
                            {
                                foreach (var kv in data.KPIs)
                                {
                                    r.ConstantItem(180).Padding(5).Border(1).BorderColor(Colors.Grey.Lighten2).Column(c2 =>
                                    {
                                        c2.Item().Text(kv.Key).FontSize(10).FontColor(Colors.Grey.Darken1);
                                        c2.Item().Text(kv.Value).Bold().FontSize(16);
                                    });
                                }
                            });
                        }

                        // Charts
                        if (data.Charts != null && data.Charts.Count > 0)
                        {
                            foreach (var kv in data.Charts)
                            {
                                col.Item().PaddingTop(8).Column(c =>
                                {
                                    c.Item().Text(kv.Key).Bold().FontSize(12);
                                    var img = GetImage(kv.Value);
                                    if (img.Length > 0)
                                    {
                                        var imageContainer = c.Item();
                                        imageContainer.Image(img, QuestPDF.Infrastructure.ImageScaling.FitWidth);
                                    }

                                    // small data table placeholder (KPIs)
                                    if (data.KPIs != null && data.KPIs.Count > 0)
                                    {
                                        c.Item().PaddingTop(6).Table(table =>
                                        {
                                            table.ColumnsDefinition(columns => { columns.RelativeColumn(); columns.RelativeColumn(); });
                                            foreach (var kvp in data.KPIs)
                                            {
                                                table.Cell().Text(kvp.Key).FontSize(9).FontColor(Colors.Grey.Darken1);
                                                table.Cell().Text(kvp.Value).FontSize(9).Bold();
                                            }
                                        });
                                    }
                                });
                            }
                        }

                    });

                    page.Footer().AlignCenter().Text($"Generated: {System.DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC").FontSize(9);
                });
            });

            using var ms = new MemoryStream();
            doc.GeneratePdf(ms);
            return ms.ToArray();
        }

        private byte[] GetImage(string base64Data)
        {
            if (string.IsNullOrEmpty(base64Data)) return new byte[0];
            // base64Data may include data:image/png;base64,... prefix
            var idx = base64Data.IndexOf("base64,");
            var data = idx >= 0 ? base64Data.Substring(idx + 7) : base64Data;
            return System.Convert.FromBase64String(data);
        }
    }
}

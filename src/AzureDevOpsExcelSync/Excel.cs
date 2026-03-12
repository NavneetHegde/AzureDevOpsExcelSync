partial class Program
{
    // ════════════════════════════════════════════════════════════
    // Excel
    // ════════════════════════════════════════════════════════════
    static void BuildExcelMulti(List<(WorkItem Story, List<WorkItem> Tasks)> groups,
        string path, string org, string project, string rootType, string rootTitle)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Work Items");

        // Headers
        ws.Cell(1, 1).Value = "ID";
        ws.Cell(1, 2).Value = "Type";
        ws.Cell(1, 3).Value = "ADO Link";
        for (int i = 0; i < FriendlyHeaders.Length; i++)
            ws.Cell(1, i + 4).Value = FriendlyHeaders[i];

        var hdr = ws.Row(1);
        hdr.Style.Font.Bold = true;
        hdr.Style.Font.FontColor = XLColor.White;
        hdr.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A5F");
        hdr.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        hdr.Height = 20;

        int row = 2;
        bool alt = false;

        foreach (var (story, tasks) in groups)
        {
            // Parent item row — blue background
            var storyType = story.Fields.TryGetValue("System.WorkItemType", out var st) ? st?.ToString() ?? "Work Item" : "Work Item";
            WriteRow(ws, row++, story, storyType, org, project, XLColor.FromHtml("#D6E4F0"));

            // Child item rows — alternating shading
            foreach (var t in tasks)
            {
                var taskType = t.Fields.TryGetValue("System.WorkItemType", out var tt) ? tt?.ToString() ?? "Work Item" : "Work Item";
                WriteRow(ws, row++, t, taskType, org, project, alt ? XLColor.FromHtml("#EEF4FB") : XLColor.FromHtml("#FAFAFA"));
                alt = !alt;
            }

            // Thin separator row between parent items
            if (groups.IndexOf((story, tasks)) < groups.Count - 1)
            {
                ws.Row(row).Height = 6;
                ws.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#E0E0E0");
                row++;
            }
        }

        ws.Columns().AdjustToContents(8, 80);
        ws.Column(1).Width = 8;
        ws.Column(2).Width = 12;
        ws.Column(3).Width = 40;
        ws.SheetView.FreezeRows(1);

        // Summary sheet
        var summary = wb.AddWorksheet("Summary");
        summary.Cell(1, 1).Value = $"{rootType}: {rootTitle}";
        summary.Cell(1, 1).Style.Font.Bold = true;
        summary.Cell(1, 1).Style.Font.FontSize = 12;
        summary.Cell(2, 1).Value = $"Parent Items: {groups.Count}";
        summary.Cell(3, 1).Value = $"Child Items:  {groups.Sum(g => g.Tasks.Count)}";
        summary.Cell(4, 1).Value = $"Pulled:       {DateTime.Now:yyyy-MM-dd HH:mm}";
        summary.Column(1).Width = 40;

        // Instructions sheet
        var info = wb.AddWorksheet("Instructions");
        string[] lines = [
            "HOW TO USE THIS FILE",
            "",
            "1. In AES, run:  /pull <id>",
            "2. Edit the YELLOW cells only. Do NOT change ID, Type or ADO Link.",
            "3. In AES, run:  /push <filename.xlsx>",
            "",
            "Yellow = editable    Grey = read-only    Dark row = separator between parent items",
            "",
            "TIPS",
            "  • Blank cell clears that field in Azure DevOps",
            "  • Save the file before /push",
            "  • Add --force to skip confirmation prompt",
        ];
        for (int i = 0; i < lines.Length; i++)
        {
            info.Cell(i + 1, 1).Value = lines[i];
            if (i == 0) info.Cell(i + 1, 1).Style.Font.Bold = true;
        }
        info.Column(1).Width = 80;

        wb.SaveAs(path);
    }

    static void BuildExcel(WorkItem story, List<WorkItem> tasks, string path, string org, string project)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Work Items");

        ws.Cell(1, 1).Value = "ID";
        ws.Cell(1, 2).Value = "Type";
        ws.Cell(1, 3).Value = "ADO Link";
        for (int i = 0; i < FriendlyHeaders.Length; i++)
            ws.Cell(1, i + 4).Value = FriendlyHeaders[i];

        var hdr = ws.Row(1);
        hdr.Style.Font.Bold = true;
        hdr.Style.Font.FontColor = XLColor.White;
        hdr.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A5F");
        hdr.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        hdr.Height = 20;

        int row = 2;
        var storyType = story.Fields.TryGetValue("System.WorkItemType", out var st) ? st?.ToString() ?? "Work Item" : "Work Item";
        WriteRow(ws, row++, story, storyType, org, project, XLColor.FromHtml("#D6E4F0"));
        bool alt = false;
        foreach (var t in tasks)
        {
            var taskType = t.Fields.TryGetValue("System.WorkItemType", out var tt) ? tt?.ToString() ?? "Work Item" : "Work Item";
            WriteRow(ws, row++, t, taskType, org, project, alt ? XLColor.FromHtml("#EEF4FB") : XLColor.FromHtml("#FAFAFA"));
            alt = !alt;
        }

        ws.Columns().AdjustToContents(8, 80);
        ws.Column(1).Width = 8;
        ws.Column(2).Width = 12;
        ws.Column(3).Width = 40;
        ws.SheetView.FreezeRows(1);

        var info = wb.AddWorksheet("Instructions");
        string[] lines = [
            "HOW TO USE THIS FILE",
            "",
            "1. In AES, run:  /pull <id>",
            "2. Edit the YELLOW cells. Do NOT change ID, Type or ADO Link.",
            "3. In AES, run:  /push <filename.xlsx>",
            "",
            "Yellow = editable    Grey = read-only",
            "",
            "TIPS",
            "  • Blank cell clears that field in Azure DevOps",
            "  • Save the file before /push",
            "  • Add --force to skip confirmation prompt",
        ];
        for (int i = 0; i < lines.Length; i++)
        {
            info.Cell(i + 1, 1).Value = lines[i];
            if (i == 0) info.Cell(i + 1, 1).Style.Font.Bold = true;
        }
        info.Column(1).Width = 80;
        wb.SaveAs(path);
    }

    static void WriteRow(IXLWorksheet ws, int row, WorkItem wi, string type,
                         string org, string project, XLColor bg)
    {
        int id = wi.Id!.Value;
        var url = $"{org.TrimEnd('/')}/{Uri.EscapeDataString(project)}/_workitems/edit/{id}";

        ws.Cell(row, 1).Value = id;
        ws.Cell(row, 2).Value = type;

        var link = ws.Cell(row, 3);
        link.Value = url;
        link.SetHyperlink(new XLHyperlink(url));
        link.Style.Font.FontColor = XLColor.FromHtml("#2E75B6");
        link.Style.Font.Underline = XLFontUnderlineValues.Single;

        for (int c = 1; c <= 3; c++)
        {
            ws.Cell(row, c).Style.Fill.BackgroundColor = XLColor.FromHtml("#F0F0F0");
            ws.Cell(row, c).Style.Font.Italic = true;
        }

        var borderColor = XLColor.FromHtml("#2E75B6");
        for (int i = 0; i < EditableFields.Length; i++)
        {
            var cell = ws.Cell(row, i + 4);
            var val = wi.Fields.TryGetValue(EditableFields[i], out var v) ? v?.ToString() ?? "" : "";
            if (EditableFields[i] == "System.AssignedTo" && wi.Fields.TryGetValue(EditableFields[i], out var raw))
                val = (raw as IdentityRef)?.DisplayName ?? raw?.ToString() ?? "";
            cell.Value = val;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#FFF9C4");
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            cell.Style.Border.OutsideBorderColor = borderColor;
            cell.Style.Alignment.WrapText = true;
        }
        ws.Row(row).Height = 18;
    }
}

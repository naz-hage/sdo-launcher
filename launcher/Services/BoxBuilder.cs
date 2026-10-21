using System;
using System.Collections.Generic;
using System.Text;

namespace Launcher.Services
{
    /// <summary>
    /// A utility class for building and displaying text-based boxes with headers and rows.
    /// </summary>
    public class BoxBuilder
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BoxBuilder"/> class with the specified headers.
        /// </summary>
        /// <param name="headers">The headers for the columns of the box.</param>
        /// <remarks>
        /// The headers define the structure of the box and determine the number of columns.
        /// </remarks>
        public BoxBuilder(params string[] headers)
        {
            _headers = headers;
        }
        private readonly List<(string[] Columns, ConsoleColor? Color)> _rows = new();
        private readonly string[] _headers;

        public BoxBuilder(params string[] headers)
        {
            _headers = headers;
        }

        /// <summary>
        /// Adds a row without specifying a color.
        /// </summary>
        /// <param name="columns">The column values for the row.</param>
        /// <remarks>
        /// The row will be added without any specific color.
        /// </remarks>
        public void AddRow(params string[] columns)
        {
            _rows.Add((columns, null));
        }

        /// <summary>
        /// Adds a row with a specified color.
        /// </summary>
        /// <param name="color">The color to apply to the row.</param>
        /// <param name="columns">The column values for the row.</param>
        public void AddRow(ConsoleColor color, params string[] columns)
        {
            _rows.Add((columns, color));
        }

        /// <summary>
        /// Builds the box as a string with proper borders and padding.
        /// </summary>
        /// <returns>The built box as a string.</returns>
        /// <remarks>
        /// The box will have a top border, header row, header separator, data rows, and a bottom border.
        /// </remarks>
        /// <example>
        /// var box = new BoxBuilder("Name", "Age");
        /// box.AddRow("Alice", "30");
        /// box.AddRow("Bob", "25");
        /// Console.WriteLine(box.Build());
        /// </example>
        public string Build()
        {
            int cols = _headers.Length;

            // Compute max width for each column
            int[] widths = new int[cols];
            for (int i = 0; i < cols; i++)
            {
                widths[i] = _headers[i].Length;
            }

            foreach (var row in _rows)
            {
                for (int i = 0; i < cols; i++)
                {
                    widths[i] = Math.Max(widths[i], row.Columns[i].Length);
                }
            }

            // Helper to pad text
            string Pad(string text, int width) =>
                text + new string(' ', width - text.Length);

            // Build top border
            var sb = new StringBuilder();
            sb.Append('┌');
            for (int i = 0; i < cols; i++)
            {
                sb.Append(new string('─', widths[i]));
                sb.Append(i == cols - 1 ? '┐' : '┬');
            }
            sb.AppendLine();

            // Header row
            sb.Append('│');
            for (int i = 0; i < cols; i++)
            {
                sb.Append(Pad(_headers[i], widths[i]));
                sb.Append('│');
            }
            sb.AppendLine();

            // Header separator
            sb.Append('├');
            for (int i = 0; i < cols; i++)
            {
                sb.Append(new string('─', widths[i]));
                sb.Append(i == cols - 1 ? '┤' : '┼');
            }
            sb.AppendLine();

            // Data rows
            foreach (var row in _rows)
            {
                sb.Append('│');
                for (int i = 0; i < cols; i++)
                {
                    sb.Append(Pad(row.Columns[i], widths[i]));
                    sb.Append('│');
                }
                sb.AppendLine();
            }

            // Bottom border
            sb.Append('└');
            for (int i = 0; i < cols; i++)
            {
                sb.Append(new string('─', widths[i]));
                sb.Append(i == cols - 1 ? '┘' : '┴');
            }

            return sb.ToString();
        }

        /// <summary>
        /// Writes the built box to the console, applying row-specific colors if specified.
        /// </summary>
        /// <remarks>
        /// The method calculates the row index based on the line number, skipping the top border and header rows.
        /// If a row has a specified color, it will be applied to the console output for that row.
        /// </remarks>
        public void Write()
        {
            var lines = Build().Split(Environment.NewLine);
            for (int i = 0; i < lines.Length; i++)
            {
                var rowIndex = i - 3;
                if (rowIndex < 0 ||
                    rowIndex >= _rows.Count ||
                    _rows[rowIndex].Color is not ConsoleColor color ||
                    Console.IsOutputRedirected)
                {
                    Console.WriteLine(lines[i]);
                    continue;
                }

                var previousColor = Console.ForegroundColor;
                try
                {
                    Console.ForegroundColor = color;
                    Console.WriteLine(lines[i]);
                }
                finally
                {
                    Console.ForegroundColor = previousColor;
                }
            }
        }
    }

}

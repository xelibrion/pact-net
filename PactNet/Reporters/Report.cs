using System;
using System.Text;
using DiffPlex;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using Newtonsoft.Json;

namespace PactNet.Reporters
{
    internal class Report
    {
        private readonly StringBuilder _stringBuilder = new StringBuilder();

        public string Build()
        {
            return _stringBuilder.ToString();
        }

        public void AddDiff(string section, object expected, object actual)
        {
            _stringBuilder.AppendLine(string.Format("{0}{1}:", Environment.NewLine, section.ToUpperInvariant()));
            _stringBuilder.Append(BuildDiff(expected, actual));
        }

        private static string BuildDiff(object expected, object actual)
        {
            var differ = new InlineDiffBuilder(new Differ());
            var diff = differ.BuildDiffModel(JsonConvert.SerializeObject(expected), JsonConvert.SerializeObject(actual));

            var builder = new StringBuilder();
            foreach (var line in diff.Lines)
            {
                if (line.Type == ChangeType.Unchanged)
                {
                    builder.AppendLine(line.Text);
                }
                else if (line.Type == ChangeType.Deleted)
                {
                    builder.AppendLine("-" + line.Text);
                }
                else if (line.Type == ChangeType.Inserted)
                {
                    builder.AppendLine("+" + line.Text);
                }
            }
            return builder.ToString();
        }
    }
}
using System.Text.Json.Nodes;

namespace Test1Data.LineProcess
{
    internal class CsvFileProcessor : IFileProcessor
    {
        public (JsonArray result, int totalLines, int badLines) process(FileStream fileStream)
        {
            using StreamReader sr = new(fileStream);
            return ProcessHelper.processLikeTxt(new ReaderEnumerable { Reader = sr }
            // skip one line because it's CSV
            .Skip(1));
        }
    }
}

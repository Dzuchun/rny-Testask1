using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Test1Data.LineProcess
{
    internal class TxtFileProcessor : IFileProcessor
    {
        public (JsonArray result, int totalLines, int badLines) process(FileStream fileStream)
        {
            using StreamReader sr = new(fileStream);
            return ProcessHelper.processLikeTxt(new ReaderEnumerable { Reader = sr });
        }
    }
}

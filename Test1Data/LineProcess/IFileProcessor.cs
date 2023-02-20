using System.Text.Json.Nodes;

namespace Test1Data.LineProcess
{
    public interface IFileProcessor
    {
        /// <summary>
        /// Refines specified file to a JSON object array. FileStream argument is used, to assure maximum freedom on the way implementation decides to read the data.
        /// </summary>
        /// <param name="fileStream">Stream of a file needed to be converted</param>
        /// <returns>Touple of resulting JSONArray, total number of lines, and error-holding lines</returns>
        public (JsonArray result, int totalLines, int badLines) process(FileStream fileStream);
    }
}

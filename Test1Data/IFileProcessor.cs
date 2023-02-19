using System.Text.Json.Nodes;

namespace Test1Data
{
    public interface IFileProcessor
    {
        /// <summary>
        /// Refines specified file to a JSON object array.
        /// </summary>
        /// <param name="stream">File to read.</param>
        /// <returns>JSON object array containing required information</returns>
        public JsonArray process(Stream stream);
    }
}

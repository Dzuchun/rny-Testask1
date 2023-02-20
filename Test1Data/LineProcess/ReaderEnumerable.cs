using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test1Data.LineProcess
{
    public class ReaderEnumerable : IEnumerable<string>
    {
        public StreamReader Reader { private get; init; } = null!;

        public IEnumerator<string> GetEnumerator()
        {
            string? current;
            while ((current = Reader.ReadLine()) is not null)
            {
                yield return current;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}

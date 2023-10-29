using System.Collections.Generic;

namespace Elk.ReadLine;

public interface ISearchHandler
{
    IEnumerable<string> Search(string query);
}
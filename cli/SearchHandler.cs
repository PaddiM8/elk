using System.Collections.Generic;
using System.Linq;
using Elk.Cli.Database;
using Elk.ReadLine;

namespace Elk.Cli;

class SearchHandler : ISearchHandler
{
    private readonly HistoryRepository _historyRepository;

    public SearchHandler(HistoryRepository historyRepository)
    {
        _historyRepository = historyRepository;
    }

    public IEnumerable<string> Search(string query)
    {
        if (query.Length == 0)
        {
            return _historyRepository
                .GetAll(100)
                .Select(x => x.Content);
        }

        return _historyRepository
            .Search(query)
            .Select(x => x.Content);
    }
}
using System.Collections.Generic;

namespace NzbDrone.Core.IndexerSearch.Definitions
{
    public class AnimeSeasonSearchCriteria : SearchCriteriaBase
    {
        public int SeasonNumber { get; set; }

        /// <summary>
        /// Snapshot of broad title-only queries already emitted earlier in this series search.
        /// Request generators should treat this as read-only state.
        /// </summary>
        public IReadOnlyCollection<string> BroadQueriesEmitted { get; set; }

        public override string ToString()
        {
            return $"[{Series.Title} : S{SeasonNumber:00}]";
        }
    }
}

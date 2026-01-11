using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LagoVista.AI.Quality.Interfaces
{
    /// <summary>
    /// Service responsible for applying batched updates to RESX files.
    /// </summary>
    public interface IResxUpdateService
    {
        /// <summary>
        /// Apply a set of key/value updates to the specified RESX file. The
        /// implementation is responsible for loading the XML, updating the
        /// &lt;data&gt; elements, and saving the file back to disk.
        /// </summary>
        /// <param name="resxPath">Physical path to the RESX file.</param>
        /// <param name="updates">Map of resourceKey -&gt; new value.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task ApplyUpdatesAsync(
            string resxPath,
            IReadOnlyDictionary<string, string> updates,
            CancellationToken cancellationToken);
    }
}

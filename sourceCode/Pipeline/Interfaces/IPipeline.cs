using System.Threading;
using System.Threading.Tasks;

namespace MnestixCore.AasGenerator.Interfaces;

public interface IPipeline<TContext>
{
    Task<TContext> RunAsync(TContext context);
}

using System;
using Game339.Shared.Infrastructure.DependencyInjection;
using Game339.Shared.Infrastructure.DependencyInjection.Implementation;
using Game339.Shared.Infrastructure.Diagnostics;

namespace Game.Runtime
{
    public static class ServiceResolver
    {
        public static T Resolve<T>() => Container.Value.Resolve<T>();

        private static readonly Lazy<IMiniContainer> Container = new (() =>
        {
            var container = new MiniContainer();

            var logger = new UnityGameLogger();
            container.RegisterSingletonInstance<IGameLog>(logger);

            // TODO DYLAN YOU(R) STUFF GOES HERE!!!
            
            return container;
        });
    }
}

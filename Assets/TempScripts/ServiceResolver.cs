// ─────────────────────────────────────────────────────────────────────────────
//  ServiceResolver.cs
//
//  Static DI container entry point. All runtime services are registered here
//  once (lazy, on first Resolve call) and handed out by interface.
//
//  CHANGES FROM ORIGINAL:
//    • Registered IInventoryModel → InventoryModel as a singleton.
//      Nothing should new-up InventoryModel directly at runtime anymore —
//      always resolve it through the container so every system shares the
//      same instance.
//
//  HOW TO ADD YOUR OWN SERVICE:
//    1. Define an interface (e.g. IMyService) with zero Unity dependencies.
//    2. Implement it (e.g. MyService : IMyService).
//    3. Add:  container.RegisterSingletonInstance<IMyService>(new MyService());
//       in the block below marked "Register game services here".
// ─────────────────────────────────────────────────────────────────────────────
using System;
using Game339.Shared.Infrastructure.DependencyInjection;
using Game339.Shared.Infrastructure.DependencyInjection.Implementation;
using Game339.Shared.Infrastructure.Diagnostics;

namespace Game.Runtime
{
    public static class ServiceResolver
    {
        public static T Resolve<T>() => Container.Value.Resolve<T>();

        private static readonly Lazy<IMiniContainer> Container = new(() =>
        {
            var container = new MiniContainer();

            // ── Infrastructure ────────────────────────────────────────────────
            var logger = new UnityGameLogger();
            container.RegisterSingletonInstance<IGameLog>(logger);

            // ── Register game services here ───────────────────────────────────
            var inventory = new InventoryModel();
            container.RegisterSingletonInstance<IInventoryModel>(inventory);

            // TODO: register additional game services below this line
            
            return container;
        });
    }
}

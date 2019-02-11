using Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;
using Autofac;
using Microsoft.Extensions.DependencyInjection;
using Aspenlaub.Net.GitHub.CSharp.Nuclide;

namespace Aspenlaub.Net.GitHub.CSharp.Fusion {
    public static class FusionContainerBuilder {
        public static ContainerBuilder UseFusionNuclideProtchAndGitty(this ContainerBuilder builder) {
            builder.UseNuclideProtchAndGitty();
            builder.RegisterType<NugetPackageUpdater>().As<INugetPackageUpdater>();
            return builder;
        }
        // ReSharper disable once UnusedMember.Global
        public static IServiceCollection UseFusionNuclideProtchAndGitty(this IServiceCollection services) {
            services.UseNuclideProtchAndGitty();
            services.AddTransient<INugetPackageUpdater, NugetPackageUpdater>();
            return services;
        }
    }
}

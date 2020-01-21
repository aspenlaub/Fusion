using Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;
using Autofac;
using Microsoft.Extensions.DependencyInjection;
using Aspenlaub.Net.GitHub.CSharp.Nuclide;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Components;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;
// ReSharper disable UnusedMember.Global

namespace Aspenlaub.Net.GitHub.CSharp.Fusion {
    public static class FusionContainerBuilder {
        public static IContainer CreateContainerUsingFusionNuclideProtchAndGitty() {
            return new ContainerBuilder().UseFusionNuclideProtchAndGitty(new DummyCsArgumentPrompter()).Build();
        }

        public static ContainerBuilder UseFusionNuclideProtchAndGitty(this ContainerBuilder builder, ICsArgumentPrompter csArgumentPrompter) {
            builder.UseNuclideProtchGittyAndPegh(csArgumentPrompter);
            builder.RegisterType<NugetPackageUpdater>().As<INugetPackageUpdater>();
            builder.RegisterType<AutoCommitterAndPusher>().As<IAutoCommitterAndPusher>();
            builder.RegisterType<FolderUpdater>().As<IFolderUpdater>();
            builder.RegisterType<ChangedBinariesLister>().As<IChangedBinariesLister>();
            builder.RegisterType<CakeReinstaller>().As<ICakeReinstaller>();
            builder.RegisterType<CakeBuilder>().As<ICakeBuilder>();
            builder.RegisterType<BinariesHelper>().As<IBinariesHelper>();
            return builder;
        }

        public static IServiceCollection UseFusionNuclideProtchAndGitty(this IServiceCollection services, ICsArgumentPrompter csArgumentPrompter) {
            services.UseNuclideProtchGittyAndPegh(csArgumentPrompter);
            services.AddTransient<INugetPackageUpdater, NugetPackageUpdater>();
            services.AddTransient<IAutoCommitterAndPusher, AutoCommitterAndPusher>();
            services.AddTransient<IFolderUpdater, FolderUpdater>();
            services.AddTransient<IChangedBinariesLister, ChangedBinariesLister>();
            services.AddTransient<ICakeReinstaller, CakeReinstaller>();
            services.AddTransient<ICakeBuilder, CakeBuilder>();
            services.AddTransient<IBinariesHelper, BinariesHelper>();
            return services;
        }
    }
}

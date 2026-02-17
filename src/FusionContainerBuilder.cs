using Aspenlaub.Net.GitHub.CSharp.Fusion.Components;
using Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Nuclide;
using Autofac;
using Microsoft.Extensions.DependencyInjection;

// ReSharper disable UnusedMember.Global

namespace Aspenlaub.Net.GitHub.CSharp.Fusion;

public static class FusionContainerBuilder {
    public static IContainer CreateContainerUsingFusionNuclideProtchAndGitty(string applicationName) {
        return new ContainerBuilder().UseFusionNuclideProtchAndGitty(applicationName).Build();
    }

    public static ContainerBuilder UseFusionNuclideProtchAndGitty(this ContainerBuilder builder, string applicationName) {
        builder.UseNuclideProtchGittyAndPegh(applicationName);
        builder.RegisterType<NugetPackageUpdater>().As<INugetPackageUpdater>();
        builder.RegisterType<NugetPackageToPushFinder>().As<INugetPackageToPushFinder>();
        builder.RegisterType<AutoCommitterAndPusher>().As<IAutoCommitterAndPusher>();
        builder.RegisterType<FolderUpdater>().As<IFolderUpdater>();
        builder.RegisterType<ChangedBinariesLister>().As<IChangedBinariesLister>();
        builder.RegisterType<MsBuilder>().As<IMsBuilder>();
        builder.RegisterType<BinariesHelper>().As<IBinariesHelper>();
        builder.RegisterType<DotNetEfInstaller>().As<IDotNetEfInstaller>();
        builder.RegisterType<DotNetEfRunner>().As<IDotNetEfRunner>();
        builder.RegisterType<DotNetBuilder>().As<IDotNetBuilder>();
        return builder;
    }

    public static IServiceCollection UseFusionNuclideProtchAndGitty(this IServiceCollection services, string applicationName) {
        services.UseNuclideProtchGittyAndPegh(applicationName);
        services.AddTransient<INugetPackageUpdater, NugetPackageUpdater>();
        services.AddTransient<INugetPackageToPushFinder, NugetPackageToPushFinder>();
        services.AddTransient<IAutoCommitterAndPusher, AutoCommitterAndPusher>();
        services.AddTransient<IFolderUpdater, FolderUpdater>();
        services.AddTransient<IChangedBinariesLister, ChangedBinariesLister>();
        services.AddTransient<IMsBuilder, MsBuilder>();
        services.AddTransient<IBinariesHelper, BinariesHelper>();
        services.AddTransient<IDotNetEfInstaller, DotNetEfInstaller>();
        services.AddTransient<IDotNetEfRunner, DotNetEfRunner>();
        services.AddTransient<IDotNetBuilder, DotNetBuilder>();
        return services;
    }
}
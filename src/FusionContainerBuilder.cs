using Aspenlaub.Net.GitHub.CSharp.Fusion.Components;
using Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Nuclide;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Components;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;
using Autofac;
using Microsoft.Extensions.DependencyInjection;
using DotNetCakeInstaller = Aspenlaub.Net.GitHub.CSharp.Fusion.Components.DotNetCakeInstaller;
using IDotNetCakeInstaller = Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces.IDotNetCakeInstaller;

// ReSharper disable UnusedMember.Global

namespace Aspenlaub.Net.GitHub.CSharp.Fusion;

public static class FusionContainerBuilder {
    public static IContainer CreateContainerUsingFusionNuclideProtchAndGitty(string applicationName) {
        return new ContainerBuilder().UseFusionNuclideProtchAndGitty(applicationName, new DummyCsArgumentPrompter()).Build();
    }

    public static ContainerBuilder UseFusionNuclideProtchAndGitty(this ContainerBuilder builder, string applicationName, ICsArgumentPrompter csArgumentPrompter) {
        builder.UseNuclideProtchGittyAndPegh(applicationName, csArgumentPrompter);
        builder.RegisterType<NugetPackageUpdater>().As<INugetPackageUpdater>();
        builder.RegisterType<NugetPackageToPushFinder>().As<INugetPackageToPushFinder>();
        builder.RegisterType<AutoCommitterAndPusher>().As<IAutoCommitterAndPusher>();
        builder.RegisterType<FolderUpdater>().As<IFolderUpdater>();
        builder.RegisterType<ChangedBinariesLister>().As<IChangedBinariesLister>();
        builder.RegisterType<CakeBuilder>().As<ICakeBuilder>();
        builder.RegisterType<BinariesHelper>().As<IBinariesHelper>();
        builder.RegisterType<DotNetEfInstaller>().As<IDotNetEfInstaller>();
        builder.RegisterType<DotNetEfRunner>().As<IDotNetEfRunner>();
        builder.RegisterType<DotNetBuilder>().As<IDotNetBuilder>();
        builder.RegisterType<DotNetCakeInstaller>().As<IDotNetCakeInstaller>();
        return builder;
    }

    public static IServiceCollection UseFusionNuclideProtchAndGitty(this IServiceCollection services, string applicationName, ICsArgumentPrompter csArgumentPrompter) {
        services.UseNuclideProtchGittyAndPegh(applicationName, csArgumentPrompter);
        services.AddTransient<INugetPackageUpdater, NugetPackageUpdater>();
        services.AddTransient<INugetPackageToPushFinder, NugetPackageToPushFinder>();
        services.AddTransient<IAutoCommitterAndPusher, AutoCommitterAndPusher>();
        services.AddTransient<IFolderUpdater, FolderUpdater>();
        services.AddTransient<IChangedBinariesLister, ChangedBinariesLister>();
        services.AddTransient<ICakeBuilder, CakeBuilder>();
        services.AddTransient<IBinariesHelper, BinariesHelper>();
        services.AddTransient<IDotNetEfInstaller, DotNetEfInstaller>();
        services.AddTransient<IDotNetEfRunner, DotNetEfRunner>();
        services.AddTransient<IDotNetBuilder, DotNetBuilder>();
        services.AddTransient<IDotNetCakeInstaller, DotNetCakeInstaller>();
        return services;
    }
}
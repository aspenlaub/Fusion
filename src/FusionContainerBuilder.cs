﻿using Aspenlaub.Net.GitHub.CSharp.Fusion.Interfaces;
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
            return builder;
        }

        public static IServiceCollection UseFusionNuclideProtchAndGitty(this IServiceCollection services, ICsArgumentPrompter csArgumentPrompter) {
            services.UseNuclideProtchGittyAndPegh(csArgumentPrompter);
            services.AddTransient<INugetPackageUpdater, NugetPackageUpdater>();
            services.AddTransient<IAutoCommitterAndPusher, AutoCommitterAndPusher>();
            return services;
        }
    }
}

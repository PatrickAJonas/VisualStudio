﻿using System;
using System.ComponentModel.Composition;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using GitHub.Api;
using GitHub.Primitives;
using Octokit;
using IProgram = GitHub.Models.IProgram;

namespace GitHub.Services
{
    [Export(typeof(IEnterpriseCapabilitiesService))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class EnterpriseCapabilitiesService : IEnterpriseCapabilitiesService
    {
        static readonly Version MinimumOAuthVersion = new Version("2.12.2");
        readonly ISimpleApiClientFactory apiClientFactory;
        readonly IEnterpriseProbe probe;
        readonly IProgram program;

        [ImportingConstructor]
        public EnterpriseCapabilitiesService(
            ISimpleApiClientFactory apiClientFactory,
            IEnterpriseProbe probe,
            IProgram program)
        {
            this.apiClientFactory = apiClientFactory;
            this.probe = probe;
            this.program = program;
        }

        public Task<EnterpriseProbeResult> Probe(Uri enterpriseBaseUrl) => probe.Probe(enterpriseBaseUrl);

        public async Task<EnterpriseLoginMethods> ProbeLoginMethods(Uri enterpriseBaseUrl)
        {
            // It's important that we don't use our cached credentials on this connection, as they
            // may be wrong - we're trying to log in after all.
            var hostAddress = HostAddress.Create(enterpriseBaseUrl);
            var connection = new Octokit.Connection(program.ProductHeader, hostAddress.ApiUri);
            var meta = await GetMetadata(connection).ConfigureAwait(false);
            var result = EnterpriseLoginMethods.Token;

            if (meta.VerifiablePasswordAuthentication) result |= EnterpriseLoginMethods.UsernameAndPassword;

            if (meta.InstalledVersion != null)
            {
                var version = new Version(meta.InstalledVersion);
                if (version >= MinimumOAuthVersion) result |= EnterpriseLoginMethods.OAuth;
            }

            return result;
        }

        private async Task<EnterpriseMeta> GetMetadata(IConnection connection)
        {
            var endpoint = new Uri("meta", UriKind.Relative);
            var response = await connection.Get<EnterpriseMeta>(endpoint, null, null).ConfigureAwait(false);
            return response.Body;
        }

        [SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Created via Octokit reflection")]
        class EnterpriseMeta : Meta
        {
            public string InstalledVersion { get; private set; }
        }
    }
}

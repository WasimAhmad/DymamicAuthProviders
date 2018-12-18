﻿// Project: aguacongas/DymamicAuthProviders
// Copyright (c) 2018 @Olivier Lefebvre
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;

namespace Aguacongas.AspNetCore.Authentication
{
    /// <summary>
    /// Configure the DI for dynamic scheme management.
    /// </summary>
    /// <seealso cref="Microsoft.AspNetCore.Authentication.AuthenticationBuilder" />
    public class DynamicAuthenticationBuilder : AuthenticationBuilder
    {
        private readonly List<Type> _handlerTypes = new List<Type>();

        /// <summary>
        /// Gets the handler types managed by this instance.
        /// </summary>
        /// <value>
        /// The handler types.
        /// </value>
        public IEnumerable<Type> HandlerTypes { get; }

        /// <summary>
        /// Gets the type of the definition.
        /// </summary>
        /// <value>
        /// The type of the definition.
        /// </value>
        public Type DefinitionType { get; }
        /// <summary>
        /// Initializes a new instance of the <see cref="DynamicAuthenticationBuilder" /> class.
        /// </summary>
        /// <param name="services">The services.</param>
        /// <param name="definitionType">Type of the definition.</param>
        public DynamicAuthenticationBuilder(IServiceCollection services, Type definitionType): base(services)
        {
            HandlerTypes = _handlerTypes;
            DefinitionType = definitionType;
        }

        /// <summary>
        /// Adds a <see cref="T:Microsoft.AspNetCore.Authentication.RemoteAuthenticationHandler`1" /> based <see cref="T:Microsoft.AspNetCore.Authentication.AuthenticationScheme" /> that supports remote authentication
        /// which can be used by <see cref="T:Microsoft.AspNetCore.Authentication.IAuthenticationService" />.
        /// </summary>
        /// <typeparam name="TOptions">The <see cref="T:Microsoft.AspNetCore.Authentication.RemoteAuthenticationOptions" /> type to configure the handler."/&gt;.</typeparam>
        /// <typeparam name="THandler">The <see cref="T:Microsoft.AspNetCore.Authentication.RemoteAuthenticationHandler`1" /> used to handle this scheme.</typeparam>
        /// <param name="authenticationScheme">The name of this scheme.</param>
        /// <param name="displayName">The display name of this scheme.</param>
        /// <param name="configureOptions">Used to configure the scheme options.</param>
        /// <returns>
        /// The builder.
        /// </returns>
        public override AuthenticationBuilder AddRemoteScheme<TOptions, THandler>(string authenticationScheme, string displayName, Action<TOptions> configureOptions)
        {
            Services.TryAddEnumerable(ServiceDescriptor.Singleton<IPostConfigureOptions<TOptions>, EnsureUniqCallbackPath<TOptions, THandler>>());
            return base.AddRemoteScheme<TOptions, THandler>(authenticationScheme, displayName, configureOptions);
        }

        /// <summary>
        /// Adds a <see cref="T:Microsoft.AspNetCore.Authentication.AuthenticationScheme" /> which can be used by <see cref="T:Microsoft.AspNetCore.Authentication.IAuthenticationService" />.
        /// </summary>
        /// <typeparam name="TOptions">The <see cref="T:Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions" /> type to configure the handler."/&gt;.</typeparam>
        /// <typeparam name="THandler">The <see cref="T:Microsoft.AspNetCore.Authentication.AuthenticationHandler`1" /> used to handle this scheme.</typeparam>
        /// <param name="authenticationScheme">The name of this scheme.</param>
        /// <param name="displayName">The display name of this scheme.</param>
        /// <param name="configureOptions">Used to configure the scheme options.</param>
        /// <returns>
        /// The builder.
        /// </returns>
        public override AuthenticationBuilder AddScheme<TOptions, THandler>(string authenticationScheme, string displayName, Action<TOptions> configureOptions)
        {
            _handlerTypes.Add(typeof(THandler));
            Services.AddSingleton(provider => 
                new OptionsMonitorCacheWrapper<TOptions>
                (
                    provider.GetRequiredService<IOptionsMonitorCache<TOptions>>(),
                    provider.GetRequiredService<IEnumerable<IPostConfigureOptions<TOptions>>>(),
                    (name, configure) =>
                    {
                        configureOptions?.Invoke((TOptions)configure);
                    }
                )
            );
            return this;
        }

        private class EnsureUniqCallbackPath<TOptions, THandler> : IPostConfigureOptions<TOptions> where TOptions : RemoteAuthenticationOptions
        {
            private readonly AuthenticationOptions _authOptions;
            private readonly IAuthenticationSchemeProvider _schemeProvider;
            private readonly IOptionsMonitorCache<AuthenticationSchemeOptions> _monitorCache;

            public EnsureUniqCallbackPath(IOptions<AuthenticationOptions> authOptions, IAuthenticationSchemeProvider schemeProvider, IOptionsMonitorCache<AuthenticationSchemeOptions> monitorCache)
            {
                _authOptions = authOptions.Value;
                _schemeProvider = schemeProvider;
                _monitorCache = monitorCache;
            }

            public void PostConfigure(string name, TOptions options)
            {
                var schemes = _schemeProvider.GetAllSchemesAsync().GetAwaiter().GetResult();
                foreach(var scheme in schemes)
                {
                    var other = _monitorCache.GetOrAdd(scheme.Name, () => options);
                    if (other != options && other is RemoteAuthenticationOptions otherRemote && otherRemote.CallbackPath == options.CallbackPath)
                    {
                        throw new InvalidOperationException($"Callbacks paths for schemes {name} and {scheme.Name} are equals: {options.CallbackPath}");
                    }
                }
            }
        }
    }
}
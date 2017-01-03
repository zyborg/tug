﻿using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tug.Client;
using Tug.Client.Configuration;

namespace Tug.Server
{
    [TestClass]
    public class ServerProtocolTests
    {
        // We us a staic Agent ID by default, but can be overridden when building the client config
        public const string DEFAULT_AGENT_ID = "12345678-0000-0000-0000-000000000001";

        // When using the ASP.NET Core TestHost, only the URL Path is significant
        public const string DEFAULT_SERVER_URL = "http://localhost/";

        // This is coordinated with the RegKey defined in the Basic Pull Handler's root
        public const string DEFAULT_REG_KEY = "c3ea5066-ce5a-4d12-a42a-850be287b2d8";

        static TestServer _tugServer;
        static DscPullConfig _tugConfig;
        static DscPullClient _tugClient;

        [ClassInitialize]
        public static void Init(TestContext ctx)
        {
            var myPath = typeof(ServerProtocolTests).GetTypeInfo().Assembly.Location;
            var myDir = Path.GetDirectoryName(myPath);
            Directory.SetCurrentDirectory(myDir);

            var hostBuilder = new WebHostBuilder()
                    .UseLoggerFactory(AppLog.Factory)
                    .UseStartup<Startup>();
            
            _tugServer = new TestServer(hostBuilder);

            _tugConfig = BuildConfig();
            _tugClient = new DscPullClient(_tugConfig, x => _tugServer.CreateClient());
        }

        [TestMethod]
        public void TestRegisterDscAgent()
        {
            _tugClient.RegisterDscAgentAsync().Wait();
        }

        [TestMethod]
        public void TestGetDscAction()
        {
            var actions = _tugClient.GetDscActionAsync().Result;
            Assert.IsNotNull(actions, "Actions are not missing or null");

            var actionsArr = actions.ToArray();
            Assert.AreEqual(1, actionsArr.Length, "Exactly 1 action response");

            Assert.AreEqual(_tugConfig.ConfigurationNames.First(), actionsArr[0].ConfigurationName,
                    "Expected configuration name");
        }

        [TestMethod]
        public void TestGetConfiguration()
        {
            var configRoot = Path.Combine(Directory.GetCurrentDirectory(),
                    "BasicPullHandlerRoot/Configuration");
            var mofPath = Path.Combine(configRoot, "SHARED/StaticTestConfig.mof");
            var csumPath = Path.Combine(configRoot, "SHARED/FYI/StaticTestConfig.mof.checksum");
            var mofBody = File.ReadAllBytes(mofPath);
            var csumBody = File.ReadAllText(csumPath);

            var fileResult = _tugClient.GetConfiguration(_tugConfig.ConfigurationNames.First()).Result;
            Assert.IsNotNull(fileResult?.Content, "File result is not missing or null");
            CollectionAssert.AreEqual(mofBody, fileResult.Content, "File result content");
            Assert.AreEqual(csumBody, fileResult.Checksum, "Expected config checksum");
        }

        [TestMethod]
        public void TestGetModule()
        {
            var modName = "xPSDesiredStateConfiguration";
            var modVers = "5.1.0.0";
            var modulesRoot = Path.Combine(Directory.GetCurrentDirectory(),
                    "BasicPullHandlerRoot/Modules");
            var modPath = Path.Combine(modulesRoot, $"{modName}/{modVers}.zip");
            var csumPath = Path.Combine(modulesRoot, $"{modName}/FYI/{modVers}.zip.checksum");
            var modBody = File.ReadAllBytes(modPath);
            var csumBody = File.ReadAllText(csumPath);

            var fileResult = _tugClient.GetModule(modName, modVers).Result;
            Assert.IsNotNull(fileResult?.Content, "File result is not missing or null");
            CollectionAssert.AreEqual(modBody, fileResult.Content, "File result content");
            Assert.AreEqual(csumBody, fileResult.Checksum, "Expected config checksum");
        }

        private static DscPullConfig BuildConfig(bool newAgentId = false)
        {
            var config = new DscPullConfig();

            config.AgentId = newAgentId
                    ? Guid.NewGuid()
                    : Guid.Parse(DEFAULT_AGENT_ID);

            config.AgentInformation = Tug.Client.Program.ComputeAgentInformation();
            config.ConfigurationNames = new[] { "StaticTestConfig" };

            // This is required for RegKey Authz
            config.CertificateInformation = new Model.CertificateInformation
            {
                FriendlyName = "Tug.Client-Test",
                Issuer = "Tug.Client.Test",
                NotAfter = DateTime.Now.AddYears(1).ToString("O"),
                NotBefore = DateTime.Now.AddMinutes(-10).ToString("O"),
                Subject = "Tug.Client-TestNode",
                PublicKey = "U3lzdGVtLlNlY3VyaXR5LkNyeXB0b2dyYXBoeS5YNTA5Q2VydGlmaWNhdGVzLlB1YmxpY0tleQ==",
                Thumbprint = "8351F16C2B06634279F2C0287B5430452DA1CD94",
                Version = 3,
            };

            config.ConfigurationRepositoryServer = new DscPullConfig.ServerConfig
            {
                ServerUrl = new Uri(DEFAULT_SERVER_URL),
                RegistrationKey = DEFAULT_REG_KEY,
            };

            // Resource Server Endpoint URL is same as Config Server Endpoint URL
            config.ResourceRepositoryServer = config.ConfigurationRepositoryServer;

            return config;
        }
    }
}

using MetaFrm.Api.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using System.Collections.Concurrent;
using System.Net.Http.Json;

namespace MetaFrm.Config
{
    /// <summary>
    /// FactoryConfig를 API로 관리하는 클래스 입니다.
    /// </summary>
    public class DefaultFactoryConfig : IFactoryConfig
    {
        /// <summary>
        /// 키와 값의 컬렉션을 나타냅니다.
        /// </summary>
        private readonly ConcurrentDictionary<string, Lazy<Task<AssemblyAttribute?>>> _cache = [];

        /// <summary>
        /// DefaultFactoryConfig 인스턴스를 생성합니다.
        /// </summary>
        public DefaultFactoryConfig() { }


        string IFactoryConfig.GetAttribute(ICore core, string attributeName) => ((IFactoryConfig)this).GetAttributeAsync(core.GetType().FullName!, attributeName).GetAwaiter().GetResult();

        async Task<string> IFactoryConfig.GetAttributeAsync(ICore core, string attributeName) => await ((IFactoryConfig)this).GetAttributeAsync(core.GetType().FullName!, attributeName);


        string IFactoryConfig.GetAttribute<T>(ICore core, string attributeName) => ((IFactoryConfig)this).GetAttributeAsync(core.GetType().FullName + "[{0}]", attributeName).GetAwaiter().GetResult();

        async Task<string> IFactoryConfig.GetAttributeAsync<T>(ICore core, string attributeName) => await ((IFactoryConfig)this).GetAttributeAsync(core.GetType().FullName + "[{0}]", attributeName);


        List<string> IFactoryConfig.GetAttribute(ICore core, List<string> listAttributeName)
        {
            List<string> vs = [];

            foreach (var attribute in listAttributeName)
                vs.Add((this as IFactoryConfig).GetAttribute(core, attribute));

            return vs;
        }
        async Task<List<string>> IFactoryConfig.GetAttributeAsync(ICore core, List<string> listAttributeName)
        {
            List<string> vs = [];

            foreach (var attribute in listAttributeName)
                vs.Add(await (this as IFactoryConfig).GetAttributeAsync(core, attribute));

            return vs;
        }


        List<string> IFactoryConfig.GetAttribute<T>(ICore core, List<string> listAttributeName)
        {
            List<string> vs = [];

            foreach (var attribute in listAttributeName)
                vs.Add((this as IFactoryConfig).GetAttribute<T>(core, attribute));

            return vs;
        }
        async Task<List<string>> IFactoryConfig.GetAttributeAsync<T>(ICore core, List<string> listAttributeName)
        {
            List<string> vs = [];

            foreach (var attribute in listAttributeName)
                vs.Add(await (this as IFactoryConfig).GetAttributeAsync<T>(core, attribute));

            return vs;
        }


        string IFactoryConfig.GetAttribute(string namespaceName, string attributeName) => (this as IFactoryConfig).GetAttributeAsync(namespaceName, attributeName).GetAwaiter().GetResult();

        async Task<string> IFactoryConfig.GetAttributeAsync(string namespaceName, string attributeName)
        {
            var assemblyAttribute = await this.GetOrLoadAssemblyAttributeAsync(namespaceName);

            if (assemblyAttribute == null)
                return string.Empty;

            var attr = assemblyAttribute.Attribute.FirstOrDefault(x => x.AttributeName == attributeName);

            if (attr?.AttributeValue is null or "") return string.Empty;

            return attr.IsEncrypt ? await attr.AttributeValue.AesDecryptorToBase64StringAsync(Factory.AccessKey, "MetaFrm") : attr.AttributeValue;
        }


        private async Task<AssemblyAttribute?> GetOrLoadAssemblyAttributeAsync(string namespaceName)
        {
            var lazy = _cache.GetOrAdd(namespaceName, ns => new Lazy<Task<AssemblyAttribute?>>(() => LoadAssemblyAttributeAsync(ns)));

            try
            {
                return await lazy.Value;
            }
            catch (Exception ex)
            {
                _cache.TryRemove(namespaceName, out _);

                if (Factory.Logger.IsEnabled(LogLevel.Error))
                    Factory.Logger.LogError(ex, "FactoryConfig load failed: {namespaceName}", namespaceName);

                return null;
            }
        }

        private async Task<AssemblyAttribute?> LoadAssemblyAttributeAsync(string namespaceName)
        {
            string path = Path.Combine(Factory.FolderPathDat, $"{Factory.ProjectServiceBase.ProjectID}_{Factory.ProjectServiceBase.ServiceID}_C_{namespaceName}_Attribute.dat");

            //API 시도
            try
            {
                using HttpRequestMessage httpRequestMessage = new(HttpMethod.Get, ((IFactoryConfig)this).GetPath(namespaceName))
                {
                    Headers = {
                        { HeaderNames.Accept, "text/plain" },
                        { "token", Factory.ProjectService.Token },
                    }
                };

                HttpResponseMessage httpResponseMessage = await Factory.HttpClientFactory.CreateClient().SendAsync(httpRequestMessage);

                if (httpResponseMessage.IsSuccessStatusCode)
                {
                    var data = await httpResponseMessage.Content.ReadFromJsonAsync<AssemblyAttribute>();

                    if (data != null)
                    {
                        await Factory.SaveInstanceAsync(data, path);
                        return data;
                    }
                }
            }
            catch (Exception ex)
            {
                if (Factory.Logger.IsEnabled(LogLevel.Error))
                    Factory.Logger.LogError(ex, "FactoryConfig API load failed: {namespaceName}", namespaceName);
            }

            //File fallback
            try
            {
                return await Factory.LoadInstanceAsync<AssemblyAttribute>(path);
            }
            catch (Exception ex)
            {
                if (Factory.Logger.IsEnabled(LogLevel.Error))
                    Factory.Logger.LogError(ex, "FactoryConfig file load failed: {namespaceName}", namespaceName);
            }

            return null;
        }

        string IFactoryConfig.GetPath(string namespaceName) => $"{Factory.BaseAddress}api/AssemblyAttribute?fullNamespace={namespaceName}";
    }
}
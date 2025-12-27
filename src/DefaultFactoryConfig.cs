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
        private readonly ConcurrentDictionary<string, Lazy<Task<AssemblyAttribute?>>> _cache = new();

        /// <summary>
        /// DefaultFactoryConfig 인스턴스를 생성합니다.
        /// </summary>
        public DefaultFactoryConfig() { }


        string IFactoryConfig.GetAttribute(ICore core, string attributeName) => (this as IFactoryConfig).GetAttribute(core.GetType().FullName!, attributeName);
        async Task<string> IFactoryConfig.GetAttributeAsync(ICore core, string attributeName) => await (this as IFactoryConfig).GetAttributeAsync(core.GetType().FullName!, attributeName);


        string IFactoryConfig.GetAttribute<T>(ICore core, string attributeName)
        {
            Type type = core.GetType();

            return (this as IFactoryConfig).GetAttribute($"{type.Namespace}.{type.Name}" + "[{0}]", attributeName);
        }
        async Task<string> IFactoryConfig.GetAttributeAsync<T>(ICore core, string attributeName)
        {
            Type type = core.GetType();

            return await (this as IFactoryConfig).GetAttributeAsync($"{type.Namespace}.{type.Name}" + "[{0}]", attributeName);
        }


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

        string IFactoryConfig.GetAttribute(string namespaceName, string attributeName)
        {
            return this.GetAttributeRunAsync(namespaceName, attributeName).GetAwaiter().GetResult();
        }
        async Task<string> IFactoryConfig.GetAttributeAsync(string namespaceName, string attributeName)
        {
            return await this.GetAttributeRunAsync(namespaceName, attributeName);
        }

        async Task<string> GetAttributeRunAsync(string namespaceName, string attributeName)
        {
            AssemblyAttribute? assembly = await this.GetOrLoadAsync(namespaceName).ConfigureAwait(false);

            if (assembly == null) return string.Empty;

            Api.Models.Attribute? attr = assembly.Attribute.FirstOrDefault(x => x.AttributeName == attributeName);

            if (attr?.AttributeValue is null or "") return string.Empty;

            return attr.IsEncrypt ? await attr.AttributeValue.AesDecryptorToBase64StringAsync(Factory.AccessKey, "MetaFrm").ConfigureAwait(false) : attr.AttributeValue;
        }

        private async Task<AssemblyAttribute?> GetOrLoadAsync(string namespaceName)
        {
            var lazy = this._cache.GetOrAdd(namespaceName, ns => new Lazy<Task<AssemblyAttribute?>>(() => this.LoadAsync(ns)));

            try
            {
                return await lazy.Value.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                this._cache.TryRemove(namespaceName, out _);

                if (Factory.Logger.IsEnabled(LogLevel.Error))
                    Factory.Logger.LogError(ex, "FactoryConfig load failed: {namespaceName}", namespaceName);

                return null;
            }
        }
        private async Task<AssemblyAttribute?> LoadAsync(string namespaceName)
        {
            string path = GetCachePath(namespaceName);

            //API
            try
            {
                using HttpRequestMessage httpRequestMessage = new(HttpMethod.Get, ((IFactoryConfig)this).GetPath(namespaceName))
                {
                    Headers = {
                        { HeaderNames.Accept, "text/plain" },
                        { "token", Factory.ProjectService.Token },
                    }
                };

                HttpResponseMessage httpResponseMessage;

                httpResponseMessage = await Factory.HttpClientFactory.CreateClient().SendAsync(httpRequestMessage).ConfigureAwait(false);

                if (httpResponseMessage.IsSuccessStatusCode)
                {
                    var data = await httpResponseMessage.Content.ReadFromJsonAsync<AssemblyAttribute>().ConfigureAwait(false);

                    if (data != null)
                    {
                        await Factory.SaveInstanceAsync(data, path).ConfigureAwait(false);

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
                return await Factory.LoadInstanceAsync<AssemblyAttribute>(path).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (Factory.Logger.IsEnabled(LogLevel.Error))
                    Factory.Logger.LogError(ex, "FactoryConfig file load failed: {namespaceName}", namespaceName);
            }

            return null;
        }

        string IFactoryConfig.GetPath(string namespaceName)
        {
            return $"{Factory.BaseAddress}api/AssemblyAttribute?fullNamespace={namespaceName}";
        }

        private static string GetCachePath(string namespaceName)
        {
            return Path.Combine(Factory.FolderPathDat, $"{Factory.ProjectServiceBase.ProjectID}_{Factory.ProjectServiceBase.ServiceID}_C_{namespaceName}_Attribute.dat");
        }
    }
}
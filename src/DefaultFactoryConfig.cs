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
        static readonly Lock lockObject = new();
        /// <summary>
        /// 키와 값의 컬렉션을 나타냅니다.
        /// </summary>
        private readonly ConcurrentDictionary<string, AssemblyAttribute> _cache = [];

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

        private bool _isFirst = true;
        string IFactoryConfig.GetAttribute(string namespaceName, string attributeName)
        {
            string path = Path.Combine(Factory.FolderPathDat, $"{Factory.ProjectServiceBase.ProjectID}_{Factory.ProjectServiceBase.ServiceID}_C_{namespaceName}_Attribute.dat");

            try
            {
                if (this._isFirst)
                    lock (lockObject)
                    {
                        if (this._cache.TryGetValue(namespaceName, out AssemblyAttribute? value1))
                        {
                            Api.Models.Attribute? attribute = value1.Attribute.SingleOrDefault(x => x.AttributeName == attributeName);

                            if (attribute != null && attribute.AttributeValue != null && attribute.AttributeValue != "")
                                return attribute.IsEncrypt ? attribute.AttributeValue.AesDecryptorToBase64String(Factory.AccessKey, "MetaFrm") : attribute.AttributeValue;
                            else
                                return "";
                        }

                        AssemblyAttribute? assemblyAttribute = this.LoadAssemblyAttributeAsync(namespaceName).Result;

                        if (assemblyAttribute != null)
                            return ((IFactoryConfig)this).GetAttribute(namespaceName, attributeName);

                        this._isFirst = false;
                    }
                else
                {
                    lock (lockObject)
                        return (this as IFactoryConfig).GetAttributeAsync(namespaceName, attributeName).GetAwaiter().GetResult();
                }
            }
            catch (Exception ex)
            {
                if (Factory.Logger.IsEnabled(LogLevel.Error))
                    Factory.Logger.LogError(ex, "IFactoryConfig.GetAttribute Exception : {namespaceName}", namespaceName);

                if (!this._cache.TryAdd(namespaceName, Factory.LoadInstance<AssemblyAttribute>(path)) && Factory.Logger.IsEnabled(LogLevel.Error))
                    Factory.Logger.LogError(ex, "IFactoryConfig.GetAttribute Exception TryAdd Fail : {namespaceName}", namespaceName);
            }

            return "";
        }
        async Task<string> IFactoryConfig.GetAttributeAsync(string namespaceName, string attributeName)
        {
            string path = Path.Combine(Factory.FolderPathDat, $"{Factory.ProjectServiceBase.ProjectID}_{Factory.ProjectServiceBase.ServiceID}_C_{namespaceName}_Attribute.dat");

            try
            {
                if (this._cache.TryGetValue(namespaceName, out AssemblyAttribute? value1))
                {
                    Api.Models.Attribute? attribute = value1.Attribute.SingleOrDefault(x => x.AttributeName == attributeName);

                    if (attribute != null && attribute.AttributeValue != null && attribute.AttributeValue != "")
                        return attribute.IsEncrypt ? await attribute.AttributeValue.AesDecryptorToBase64StringAsync(Factory.AccessKey, "MetaFrm") : attribute.AttributeValue;
                    else
                        return "";
                }

                AssemblyAttribute? assemblyAttribute = await this.LoadAssemblyAttributeAsync(namespaceName);

                if (assemblyAttribute != null)
                    return await ((IFactoryConfig)this).GetAttributeAsync(namespaceName, attributeName);
            }
            catch (Exception ex)
            {
                if (Factory.Logger.IsEnabled(LogLevel.Error))
                    Factory.Logger.LogError(ex, "IFactoryConfig.GetAttributeAsync Exception : {namespaceName}", namespaceName);
            }

            return "";
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
                    AssemblyAttribute? assemblyAttribute;
                    assemblyAttribute = await httpResponseMessage.Content.ReadFromJsonAsync<AssemblyAttribute>();

                    if (assemblyAttribute != null)
                    {
                        if (!this._cache.TryAdd(namespaceName, assemblyAttribute) && Factory.Logger.IsEnabled(LogLevel.Error))
                            Factory.Logger.LogError("IFactoryConfig.GetAttributeAsync Attribute TryAdd Fail : {namespaceName}", namespaceName);

                        await Factory.SaveInstanceAsync(assemblyAttribute, path);

                        return assemblyAttribute;
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
                AssemblyAttribute assemblyAttribute = Factory.LoadInstance<AssemblyAttribute>(path);

                if (!this._cache.TryAdd(namespaceName, assemblyAttribute) && Factory.Logger.IsEnabled(LogLevel.Error))
                    Factory.Logger.LogError("IFactoryConfig.GetAttribute Exception TryAdd Fail : {namespaceName}", namespaceName);

                return assemblyAttribute;
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
    }
}
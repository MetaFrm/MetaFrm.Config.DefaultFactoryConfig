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
        static readonly object lockObject = new();
        /// <summary>
        /// 키와 값의 컬렉션을 나타냅니다.
        /// </summary>
        private ConcurrentDictionary<string, AssemblyAttribute> Attribute { get; set; } = [];

        /// <summary>
        /// DefaultFactoryConfig 인스턴스를 생성합니다.
        /// </summary>
        public DefaultFactoryConfig() { }


        string IFactoryConfig.GetAttribute(ICore core, string attributeName)
        {
            return (this as IFactoryConfig).GetAttribute($"{core.GetType().FullName}", attributeName);
        }
        async Task<string> IFactoryConfig.GetAttributeAsync(ICore core, string attributeName)
        {
            return await (this as IFactoryConfig).GetAttributeAsync($"{core.GetType().FullName}", attributeName);
        }


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
            string path = Path.Combine(Factory.FolderPathDat, $"{Factory.ProjectServiceBase?.ProjectID}_{Factory.ProjectServiceBase?.ServiceID}_C_{namespaceName}_Attribute.dat");

            try
            {
                lock (lockObject)
                {
                    if (this.Attribute.TryGetValue(namespaceName, out AssemblyAttribute? value1))
                    {
                        Api.Models.Attribute? attribute = value1.Attribute.SingleOrDefault(x => x.AttributeName == attributeName);

                        if (attribute != null && attribute.AttributeValue != null && attribute.AttributeValue != "")
                            return attribute.IsEncrypt ? attribute.AttributeValue.AesDecryptorToBase64String(Factory.AccessKey, "MetaFrm") : attribute.AttributeValue;
                        else
                            return "";
                    }

                    HttpRequestMessage httpRequestMessage = new(HttpMethod.Get, ((IFactoryConfig)this).GetPath(namespaceName))
                    {
                        Headers = {
                        { HeaderNames.Accept, "text/plain" },
                        { "token", Factory.ProjectService.Token },
                    }
                    };

                    HttpResponseMessage httpResponseMessage = Factory.HttpClientFactory.CreateClient().SendAsync(httpRequestMessage).Result;

                    if (httpResponseMessage.IsSuccessStatusCode)
                    {
                        AssemblyAttribute? assemblyAttribute;
                        assemblyAttribute = httpResponseMessage.Content.ReadFromJsonAsync<AssemblyAttribute>().Result;

                        if (assemblyAttribute != null)
                        {
                            if (!this.Attribute.TryAdd(namespaceName, assemblyAttribute))
                                Factory.Logger.LogError("IFactoryConfig.GetAttribute Attribute TryAdd Fail : {namespaceName}", namespaceName);

                            Factory.SaveInstance(assemblyAttribute, path);

                            return ((IFactoryConfig)this).GetAttribute(namespaceName, attributeName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Factory.Logger.LogError(ex, "IFactoryConfig.GetAttribute Exception : {namespaceName}", namespaceName);
                if (!this.Attribute.TryAdd(namespaceName, Factory.LoadInstance<AssemblyAttribute>(path)))
                    Factory.Logger.LogError(ex, "IFactoryConfig.GetAttribute Exception TryAdd Fail : {namespaceName}", namespaceName);
            }

            return "";
        }
        async Task<string> IFactoryConfig.GetAttributeAsync(string namespaceName, string attributeName)
        {
            string path = Path.Combine(Factory.FolderPathDat, $"{Factory.ProjectServiceBase?.ProjectID}_{Factory.ProjectServiceBase?.ServiceID}_C_{namespaceName}_Attribute.dat");

            try
            {
                if (this.Attribute.TryGetValue(namespaceName, out AssemblyAttribute? value1))
                {
                    Api.Models.Attribute? attribute = value1.Attribute.SingleOrDefault(x => x.AttributeName == attributeName);

                    if (attribute != null && attribute.AttributeValue != null && attribute.AttributeValue != "")
                        return attribute.IsEncrypt ? await attribute.AttributeValue.AesDecryptorToBase64StringAsync(Factory.AccessKey, "MetaFrm") : attribute.AttributeValue;
                    else
                        return "";
                }

                HttpRequestMessage httpRequestMessage = new(HttpMethod.Get, ((IFactoryConfig)this).GetPath(namespaceName))
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
                        if (!this.Attribute.TryAdd(namespaceName, assemblyAttribute))
                            Factory.Logger.LogError("IFactoryConfig.GetAttributeAsync Attribute TryAdd Fail : {namespaceName}", namespaceName);

                        Factory.SaveInstanceAsync(assemblyAttribute, path);

                        return await ((IFactoryConfig)this).GetAttributeAsync(namespaceName, attributeName);
                    }
                }
            }
            catch (Exception ex)
            {
                Factory.Logger.LogError(ex, "IFactoryConfig.GetAttributeAsync Exception : {namespaceName}", namespaceName);
                if (!this.Attribute.TryAdd(namespaceName, await Factory.LoadInstanceAsync<AssemblyAttribute>(path)))
                    Factory.Logger.LogError(ex, "IFactoryConfig.GetAttributeAsync Exception TryAdd Fail : {namespaceName}", namespaceName);
            }

            return "";
        }

        string IFactoryConfig.GetPath(string namespaceName)
        {
            return $"{Factory.BaseAddress}api/AssemblyAttribute?fullNamespace={namespaceName}";
        }
    }
}
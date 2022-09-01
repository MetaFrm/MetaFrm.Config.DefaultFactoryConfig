using MetaFrm.Api.Models;
using System.Net.Http.Headers;
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
        private Dictionary<string, AssemblyAttribute> Attribute { get; set; }

        private readonly HttpClient httpClient;
        private static bool httpClientException;

        /// <summary>
        /// DefaultFactoryConfig 인스턴스를 생성합니다.
        /// </summary>
        public DefaultFactoryConfig()
        {
            this.Attribute = new Dictionary<string, AssemblyAttribute>();

            // Update port # in the following line.
            this.httpClient = new()
            {
                BaseAddress = new Uri(Factory.BaseAddress)
            };
            this.httpClient.DefaultRequestHeaders.Accept.Clear();
            this.httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));
            this.httpClient.DefaultRequestHeaders.Add("token", Factory.ProjectService.Token);
        }

        string IFactoryConfig.GetAttribute(ICore core, string attributeName)
        {
            return (this as IFactoryConfig).GetAttribute($"{core.GetType().FullName}", attributeName);
        }

        List<string> IFactoryConfig.GetAttribute(ICore core, List<string> listAttributeName)
        {
            List<string> vs = new();

            foreach (var attribute in listAttributeName)
                vs.Add((this as IFactoryConfig).GetAttribute($"{core.GetType().FullName}", attribute));

            return vs;
        }

        string IFactoryConfig.GetAttribute(string namespaceName, string attributeName)
        {
            string key = string.Format("{0}.{1}", namespaceName, attributeName);
            string path = $"{Factory.FolderPathDat}{Factory.ProjectID}_{Factory.ServiceID}_C_{namespaceName}_Attribute.dat";

            if (this.Attribute.ContainsKey(namespaceName))
            {
                Api.Models.Attribute value = this.Attribute[namespaceName].Attribute.Single(x => x.AttributeName == attributeName);

                if (value != null && value.AttributeValue != null && value.AttributeValue != "")
                    return value.IsEncrypt ? value.AttributeValue.AesDecryptorToBase64String("MetaFrm", Factory.AccessKey) : value.AttributeValue;
                else
                    return "";
            }

            if (!httpClientException)
                try
                {
                    HttpResponseMessage response = httpClient.GetAsync(((IFactoryConfig)this).GetPath(namespaceName)).Result;

                    response.EnsureSuccessStatusCode();

                    if (response.IsSuccessStatusCode)
                    {
                        AssemblyAttribute? assemblyAttribute;
                        assemblyAttribute = response.Content.ReadFromJsonAsync<AssemblyAttribute>().Result;

                        if (assemblyAttribute != null && !this.Attribute.ContainsKey(namespaceName))
                        {
                            this.Attribute.Add(namespaceName, assemblyAttribute);
                            Factory.SaveInstance(assemblyAttribute, path);
                        }
                    }
                }
                catch (HttpRequestException)
                {
                    httpClientException = true;
                    if (!this.Attribute.ContainsKey(namespaceName))
                        this.Attribute.Add(namespaceName, Factory.LoadInstance<AssemblyAttribute>(path));
                }
            else
            {
                if (!this.Attribute.ContainsKey(namespaceName))
                    this.Attribute.Add(namespaceName, Factory.LoadInstance<AssemblyAttribute>(path));
            }

            if (this.Attribute.ContainsKey(namespaceName))
            {
                string? value = this.Attribute[namespaceName].Attribute.Single(x => x.AttributeName == attributeName).AttributeValue;

                if (value != null)
                    return value;
                else
                    return "";
            }
            else
                return "";
        }

        string IFactoryConfig.GetPath(string namespaceName)
        {
            return $"{Factory.BaseAddress}api/AssemblyAttribute?fullNamespace={namespaceName}";
        }
    }
}
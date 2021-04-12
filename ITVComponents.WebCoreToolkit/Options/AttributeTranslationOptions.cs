using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Options
{
    public class AttributeTranslationOptions
    {
        private Dictionary<Type, string> attributeMappings = new Dictionary<Type, string>();

        private Dictionary<string, Type> resourceMappings = new Dictionary<string, Type>();
        
        private Dictionary<Type, Func<Attribute, string, string>> topicMappings = new Dictionary<Type, Func<Attribute, string, string>>();

        /// <summary>
        /// Removes all Attribute mappings
        /// </summary>
        public void ClearAttributeMappings()
        {
            attributeMappings.Clear();
        }
        
        /// <summary>
        /// Removes all Resource mappings
        /// </summary>
        public void ClearResourceMappings()
        {
            resourceMappings.Clear();
        }
        
        /// <summary>
        /// Removes all Resource mappings
        /// </summary>
        public void ClearDefaultTopicCallbacks()
        {
            topicMappings.Clear();
        }
        
        /// <summary>
        /// Mapps a Resource-Type to a specific Resource-Prefix
        /// </summary>
        /// <param name="attributeType">the attribute-type for which to configure the default resource-prefix for localization</param>
        /// <param name="resourcePrefix">the resource-prefix for localization</param>
        public void MapAttribute(Type attributeType, string resourcePrefix)
        {
            attributeMappings[attributeType] = resourcePrefix;
        }

        /// <summary>
        /// Mapps a Localization-Prefix to a specific resource-type that has localized resources
        /// </summary>
        /// <param name="resourcePrefix">the localization-prefix</param>
        /// <param name="resourceType">the type that contains localized resources</param>
        public void MapResource(string resourcePrefix, Type resourceType)
        {
            resourceMappings[resourcePrefix] = resourceType;
        }
        
        /// <summary>
        /// Adds a mapping callback to this options instance
        /// </summary>
        /// <param name="attributeType">the attribute-Type for which to estimate the message-topic</param>
        /// <param name="mappingCallback">the callback that may or may not alter the message-topic</param>
        public void AddTopicCallback(Type attributeType, Func<Attribute, string,string> mappingCallback, bool preserveOriginal = false)
        {
            Func<Attribute, string,string> tmp = mappingCallback;
            if (preserveOriginal && topicMappings.ContainsKey(attributeType))
            {
                var orig = topicMappings[attributeType];

                var tmpO = tmp;
                tmp = (attribute, s) => tmpO(attribute, orig(attribute,s));
            }
            
            topicMappings[attributeType] = tmp;
        }
        
        /// <summary>
        /// Creates the full-qualified resource pattern for the specified attribute topic
        /// </summary>
        /// <param name="attribute">the attribute for which to get the full-qualified resource name</param>
        /// <param name="topic">the topic of the desired message</param>
        /// <returns>the full-qualified resource identifier that can be resolved by a resource-resolver</returns>
        public string ResourceForAttribute(Attribute attribute, string topic)
        {
            string retVal = null;
            Type attributeType;
            if (attribute != null && attributeMappings.ContainsKey(attributeType=attribute.GetType()))
            {
                var prefix = attributeMappings[attributeType];
                if (topicMappings.ContainsKey(attributeType))
                {
                    topic = topicMappings[attributeType](attribute,topic);
                }
                
                retVal = $"{prefix}:{attributeType.Name}_{topic}";
            }
            
            return retVal;
        }
        
        /// <summary>
        /// Gets the Resource-Type that contains the message that belongs to the given resource
        /// </summary>
        /// <param name="fqResource">the fq-resource for which to get the appropriate type</param>
        /// <param name="resourceName">the type-internal resource-identifier</param>
        /// <returns>the type that holds the desired resource</returns>
        public Type GetResourceType(string fqResource, out string resourceName)
        {
            Type retVal = null;
            resourceName = null;
            int id = -1;
            if (!string.IsNullOrEmpty(fqResource) && (id = fqResource.IndexOf(":")) != -1)
            {
                var resourceKey = fqResource.Substring(0,id);
                if (resourceMappings.ContainsKey(resourceKey))
                {
                    resourceName = fqResource.Substring(id + 1);
                    retVal = resourceMappings[resourceKey];
                }
            }
            
            return retVal;
        }
    }
}

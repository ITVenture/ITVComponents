using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dynamitey.DynamicObjects;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Scripting.CScript.Core.Methods;
using ITVComponents.Scripting.CScript.Helpers;
using Microsoft.VisualStudio.TestPlatform.Common.ExtensionFramework.Utilities;
using RestSharp;

namespace ITVComponents.Scripting.CScript.Test
{
    [TestClass]
    public class ExtensionTests
    {
        [TestInitialize]
        public void Init()
        {
            ExtensionMethod.RegisterAllMethods(typeof(RestRequestExtensions));
            //ExtensionMethod.RegisterNonGenericMethods(typeof(RestSharp.RestRequestExtensions));
        }

        [TestMethod]
        public void TestExtensions()
        {
            using (var fubar = ExpressionParser.BeginRepl(new Dictionary<string,object>
                   {
                       {
                           "Req", new RestRequest()

                       },
                       {
                           "b", Encoding.Default.GetBytes("Fubar")
                       },
                       {
                           "t",
                           typeof(object)
                       }
                   }, a => DefaultCallbacks.PrepareDefaultCallbacks(a.Scope,a.ReplSession)))
            {
                var retVal = ExpressionParser.Parse(@"Req.AddParameter(""fastImport"",""true"")", fubar);
                Assert.AreNotEqual(null, retVal);
                retVal = ExpressionParser.Parse(@"Req.AddFile("""",b,""fubar.txt"")", fubar);
                Assert.AreNotEqual(null, retVal);
                retVal = ExpressionParser.Parse(@"Req.AddJsonBody<#t>({fubar:true})", fubar);
                Assert.AreNotEqual(null, retVal);
            }
        }
    }
}

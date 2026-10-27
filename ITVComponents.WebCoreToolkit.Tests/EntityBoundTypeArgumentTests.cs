using System;
using System.Collections.Generic;
using System.Linq;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.AspExtensions.Attributes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.WebCoreToolkit.Tests
{
    /// <summary>
    /// Ein ViewModel-Typargument folgt dem Entitaetstyp, wenn sich ein ViewModel per <see cref="ViewModelForEntityAttribute"/>
    /// zu ihm bekennt - ohne Eintrag in <c>ViewConfig:CustomViewGenericArgs</c>.
    /// </summary>
    [TestClass]
    public class EntityBoundTypeArgumentTests
    {
        [TestMethod]
        public void WithoutAClaimingViewModel_TheDefaultIsUsed()
            => Assert.AreEqual(typeof(ProbeViewModel), ViewModelFor(typeof(PlainProbeEntity)));

        [TestMethod]
        public void AViewModelClaimingAnOpenGenericEntity_IsUsedForEveryClosingAndDerivation()
        {
            Assert.AreEqual(typeof(TreeProbeViewModel), ViewModelFor(typeof(TreeProbeEntity)));
            Assert.AreEqual(typeof(TreeProbeViewModel), ViewModelFor(typeof(OwnTreeProbeEntity)));
        }

        [TestMethod]
        public void TheNearestClaim_Wins()
        {
            Assert.AreEqual(typeof(FarProbeViewModel), ViewModelFor(typeof(NearBaseProbeEntity)));
            Assert.AreEqual(typeof(NearProbeViewModel), ViewModelFor(typeof(NearDerivedProbeEntity)));
        }

        [TestMethod]
        public void TwoEqualClaims_FallBackToTheDefault()
            => Assert.AreEqual(typeof(ProbeViewModel), ViewModelFor(typeof(DisputedProbeEntity)));

        [TestMethod]
        public void ExplicitConfiguration_StillWins()
        {
            var part = new AssemblyPartWithGenerics(typeof(EntityBoundTypeArgumentTests).Assembly,
                new Dictionary<string, Type> { { "TProbeEntity", typeof(TreeProbeEntity) } },
                new Dictionary<string, Type> { { "TProbeViewModel", typeof(ConfiguredProbeViewModel) } });
            Assert.AreEqual(typeof(ConfiguredProbeViewModel), Probe(part).GetGenericArguments()[1]);
        }

        private static Type ViewModelFor(Type entityType)
        {
            var part = new AssemblyPartWithGenerics(typeof(EntityBoundTypeArgumentTests).Assembly,
                new Dictionary<string, Type> { { "TProbeEntity", entityType } });
            return Probe(part).GetGenericArguments()[1];
        }

        private static Type Probe(AssemblyPartWithGenerics part)
            => part.Types.Single(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(EntityBoundProbe<,>));
    }

    [CustomGenericTypeArg("TProbeViewModel", typeof(ProbeViewModel), EntityParameter = "TProbeEntity")]
    public class EntityBoundProbe<TProbeEntity, TProbeViewModel> where TProbeViewModel : ProbeViewModel
    {
    }

    public class ProbeViewModel
    {
    }

    public class PlainProbeEntity
    {
    }

    public class TreeProbeEntity<TKey>
    {
    }

    public class TreeProbeEntity : TreeProbeEntity<int>
    {
    }

    public class OwnTreeProbeEntity : TreeProbeEntity
    {
    }

    [ViewModelForEntity(typeof(TreeProbeEntity<>))]
    public class TreeProbeViewModel : ProbeViewModel
    {
    }

    public class ConfiguredProbeViewModel : ProbeViewModel
    {
    }

    public class NearBaseProbeEntity
    {
    }

    public class NearDerivedProbeEntity : NearBaseProbeEntity
    {
    }

    [ViewModelForEntity(typeof(NearBaseProbeEntity))]
    public class FarProbeViewModel : ProbeViewModel
    {
    }

    [ViewModelForEntity(typeof(NearDerivedProbeEntity))]
    public class NearProbeViewModel : ProbeViewModel
    {
    }

    public class DisputedProbeEntity
    {
    }

    [ViewModelForEntity(typeof(DisputedProbeEntity))]
    public class DisputedProbeViewModelA : ProbeViewModel
    {
    }

    [ViewModelForEntity(typeof(DisputedProbeEntity))]
    public class DisputedProbeViewModelB : ProbeViewModel
    {
    }
}

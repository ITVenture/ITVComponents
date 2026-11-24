// The bridge tests implement the obsolete IManualSerializer on purpose.
#pragma warning disable CS0618
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.Json;
using ITVComponents.Json.Contracts;
using ITVComponents.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.Tests
{
    /// <summary>
    /// SerializationTypingMode.EnvelopePolymorphism (issue Json 01): runtime types on every level, exact primitives,
    /// short names, version-tolerant type names, references across envelopes and the bridge for IManualSerializer.
    /// </summary>
    [TestClass]
    public class EnvelopePolymorphismTests
    {
        private const SerializationTypingMode Mode = SerializationTypingMode.EnvelopePolymorphism;

        private static void EnsureAliases()
        {
            // Idempotent: the registry is global, every test may call it.
            JsonHelper.RegisterManualType<Item>("envtest.item");
            JsonHelper.RegisterManualType<Circle>("envtest.circle");
            JsonHelper.RegisterManualType<Square>("envtest.square");
            JsonHelper.RegisterManualType<Node>("envtest.node");
            JsonHelper.RegisterManualType<Child>("envtest.child");
            JsonHelper.RegisterManualType<ManualBag>("envtest.manual-bag");
            JsonHelper.RegisterManualType<DuplicateBag>("envtest.duplicate-bag");
        }

        private static string Write<T>(T value, bool preserve = false) => JsonHelper.ToJson(value, Mode, preserve);

        private static T Read<T>(string json, bool preserve = false) => JsonHelper.FromJsonString<T>(json, Mode, preserve);

        [TestMethod]
        public void Derived_Types_Survive_In_Base_Object_List_Array_And_Dictionary_Places()
        {
            EnsureAliases();
            var holder = new Holder
            {
                Shape = new Circle { Label = "c", Radius = 2.5 },
                Anything = new Square { Label = "s", Side = 3 },
                Shapes = new List<Shape> { new Circle { Label = "c2", Radius = 1 }, new Square { Label = "s2", Side = 4 } },
                Objects = new object[] { new Item { Name = "i" }, 7 },
                Map = new Dictionary<string, object> { ["a"] = new Circle { Label = "m", Radius = 9 }, ["b"] = 12L }
            };

            var back = Read<Holder>(Write(holder));

            Assert.IsInstanceOfType<Circle>(back.Shape);
            Assert.AreEqual(2.5, ((Circle)back.Shape).Radius);
            Assert.IsInstanceOfType<Square>(back.Anything);
            Assert.AreEqual(3, ((Square)back.Anything).Side);
            Assert.IsInstanceOfType<Circle>(back.Shapes[0]);
            Assert.IsInstanceOfType<Square>(back.Shapes[1]);
            Assert.AreEqual(4, ((Square)back.Shapes[1]).Side);
            Assert.IsInstanceOfType<Item>(back.Objects[0]);
            Assert.IsInstanceOfType<int>(back.Objects[1]);
            Assert.AreEqual(9, ((Circle)back.Map["a"]).Radius);
            Assert.IsInstanceOfType<long>(back.Map["b"]);
        }

        [TestMethod]
        public void Envelopes_Nest_Over_Three_Levels()
        {
            EnsureAliases();
            var root = new Node { Name = "1", Next = new Node { Name = "2", Next = new Node { Name = "3", Next = new Item { Name = "leaf" } } } };

            var back = Read<Node>(Write(root));

            var two = (Node)back.Next;
            var three = (Node)two.Next;
            Assert.AreEqual("3", three.Name);
            Assert.AreEqual("leaf", ((Item)three.Next).Name);
        }

        [TestMethod]
        public void Concrete_Root_Has_No_Envelope_Open_Root_Has_One()
        {
            EnsureAliases();
            var concrete = JsonNode.Parse(Write(new Item { Name = "x" }))!.AsObject();
            Assert.IsTrue(concrete.ContainsKey("Name"));
            Assert.IsFalse(concrete.ContainsKey("$type"));

            var open = JsonNode.Parse(JsonHelper.ToJson<object>(new Item { Name = "x" }, Mode))!.AsObject();
            Assert.AreEqual("envtest.item", open["$type"]!.GetValue<string>());

            // Without a type the root is enveloped as well, and reads back typed.
            var untyped = JsonHelper.ToJson((object)new Item { Name = "y" }, Mode, null);
            Assert.IsTrue(JsonNode.Parse(untyped)!.AsObject().ContainsKey("$type"));
            Assert.AreEqual("y", ((Item)Read<object>(untyped)).Name);
        }

        [TestMethod]
        public void Enveloped_Root_Read_Into_Concrete_Type_Is_Unpacked_And_Checked()
        {
            EnsureAliases();
            var json = JsonHelper.ToJson<object>(new Item { Name = "x" }, Mode);

            Assert.AreEqual("x", Read<Item>(json).Name);
            Assert.ThrowsExactly<JsonException>(() => Read<Node>(json));
        }

        [TestMethod]
        public void Value_Before_Type_Is_Read()
        {
            EnsureAliases();
            var json = "{\"Anything\":{\"$value\":{\"Name\":\"late\"},\"$type\":\"envtest.item\"}}";

            Assert.AreEqual("late", ((Item)Read<Holder>(json).Anything).Name);
        }

        [TestMethod]
        public void Primitives_In_Object_Places_Keep_Their_Exact_Type()
        {
            var guid = Guid.NewGuid();
            var date = new DateTime(2026, 1, 31, 12, 0, 0, DateTimeKind.Utc);
            var values = new object[]
            {
                42, 42L, 1.5m, 2.5d, 'c', guid, date, new DateTimeOffset(date), TimeSpan.FromMinutes(3), DayOfWeek.Friday,
                "text", true, null
            };

            var json = Write(values);
            var back = Read<object[]>(json);

            Assert.AreEqual<object>(42, back[0]);
            Assert.AreEqual<object>(42L, back[1]);
            Assert.AreEqual<object>(1.5m, back[2]);
            Assert.AreEqual<object>(2.5d, back[3]);
            Assert.AreEqual<object>('c', back[4]);
            Assert.AreEqual<object>(guid, back[5]);
            Assert.AreEqual<object>(date, back[6]);
            Assert.AreEqual<object>(new DateTimeOffset(date), back[7]);
            Assert.AreEqual<object>(TimeSpan.FromMinutes(3), back[8]);
            Assert.AreEqual<object>(DayOfWeek.Friday, back[9]);
            Assert.AreEqual<object>("text", back[10]);
            Assert.AreEqual<object>(true, back[11]);
            Assert.IsNull(back[12]);

            var array = JsonNode.Parse(json)!.AsArray();
            Assert.AreEqual(JsonValueKind.String, array[10]!.GetValueKind(), "string is written without envelope");
            Assert.AreEqual(JsonValueKind.True, array[11]!.GetValueKind(), "bool is written without envelope");
            Assert.AreEqual("int", array[0]!["$type"]!.GetValue<string>());
        }

        [TestMethod]
        public void Hand_Written_Values_Without_Envelope()
        {
            var back = Read<Holder>("{\"Anything\":5000000000,\"Comparable\":7,\"Objects\":[{\"X\":1},[1,2]]}");

            Assert.AreEqual<object>(5000000000L, back.Anything);
            Assert.AreEqual<object>(7, back.Comparable);
            Assert.IsInstanceOfType<JsonObject>(back.Objects[0]);
            Assert.IsInstanceOfType<JsonArray>(back.Objects[1]);

            Assert.ThrowsExactly<JsonException>(() => Read<Holder>("{\"Shape\":{\"Label\":\"x\"}}"),
                "an object without envelope in a base-typed place");
            Assert.ThrowsExactly<JsonException>(() => Read<Holder>("{\"Shape\":\"text\"}"),
                "a string in a base-typed place");
        }

        [TestMethod]
        public void A_Plain_Object_Does_Not_Recurse()
        {
            var back = Read<object[]>(Write(new object[] { new object() }));

            Assert.AreEqual(typeof(object), back[0].GetType());
        }

        [TestMethod]
        public void Collections_Come_Back_In_Their_Exact_Type_With_Composed_Short_Names()
        {
            EnsureAliases();
            var item = new Item { Name = "i" };
            var values = new object[]
            {
                new List<Item> { item },
                new HashSet<Item> { item },
                new Dictionary<string, Item> { ["k"] = item },
                new[] { item }
            };

            var json = Write(values);
            var back = Read<object[]>(json);

            Assert.IsInstanceOfType<List<Item>>(back[0]);
            Assert.IsInstanceOfType<HashSet<Item>>(back[1]);
            Assert.IsInstanceOfType<Dictionary<string, Item>>(back[2]);
            Assert.IsInstanceOfType<Item[]>(back[3]);
            StringAssert.Contains(json, "\"list<envtest.item>\"");
            StringAssert.Contains(json, "\"set<envtest.item>\"");
            StringAssert.Contains(json, "\"dict<string,envtest.item>\"");
            StringAssert.Contains(json, "\"envtest.item[]\"");
        }

        [TestMethod]
        public void Type_Names_Without_Short_Name_Carry_No_Version_And_Resolve_Across_Versions()
        {
            var json = Write(new object[] { new Unregistered { Value = 1 }, new List<Unregistered>() });
            Assert.IsFalse(json.Contains("Version="), "no version in the written type names");
            StringAssert.Contains(json, "ITVComponents.Tests.EnvelopePolymorphismTests+Unregistered, ITVComponents.Tests");

            foreach (var version in new[] { "99.0.0.0", "0.0.0.1" })
            {
                var name = $"ITVComponents.Tests.EnvelopePolymorphismTests+Unregistered, ITVComponents.Tests, Version={version}, Culture=neutral, PublicKeyToken=null";
                var back = Read<object>($"{{\"$type\":\"{name}\",\"$value\":{{\"Value\":3}}}}");
                Assert.AreEqual(3, ((Unregistered)back).Value, $"version {version}");
            }

            Assert.IsInstanceOfType<List<Unregistered>>(Read<object[]>(json)[1]);
        }

        [TestMethod]
        public void Restriction_Accepts_Short_Names_And_Refuses_Other_Types()
        {
            EnsureAliases();
            var registered = Write(new Holder { Anything = new Item { Name = "ok" } });
            var unregistered = Write(new Holder { Anything = new Unregistered { Value = 1 }, Shape = null });
            var unregisteredArgument = Write(new Holder { Anything = new List<Unregistered>() });
            var unregisteredShape = Write(new Holder { Shape = new UnregisteredShape { Label = "x" } });

            using (JsonHelper.RestrictManualTypesToRegistered())
            {
                Assert.AreEqual("ok", ((Item)Read<Holder>(registered).Anything).Name);
                Assert.IsInstanceOfType<JsonNode>(Read<Holder>(unregistered).Anything, "object place: kept untyped");
                Assert.IsInstanceOfType<JsonNode>(Read<Holder>(unregisteredArgument).Anything, "generic argument not known");
                Assert.ThrowsExactly<JsonException>(() => Read<Holder>(unregisteredShape), "typed place: refused");
            }

            Assert.IsInstanceOfType<Unregistered>(Read<Holder>(unregistered).Anything, "without the restriction it loads");
        }

        [TestMethod]
        public void Unknown_Type_Is_Logged_And_Kept_Untyped_Or_Rejected()
        {
            var messages = new List<(string Text, LogSeverity Severity)>();
            Holder back;
            using (LogEnvironment.SetTemporaryLogHook((debug, text, severity, context) => messages.Add((text, severity))))
            {
                back = Read<Holder>("{\"Anything\":{\"$type\":\"No.Such.Type, No.Such.Assembly\",\"$value\":{\"X\":1}}}");
            }

            Assert.IsInstanceOfType<JsonObject>(back.Anything);
            Assert.AreEqual(1, ((JsonObject)back.Anything)["X"]!.GetValue<int>());
            Assert.IsTrue(messages.Any(m => m.Severity == LogSeverity.Error && m.Text.Contains("No.Such.Type")),
                "a type that could not be loaded is an error in the log");

            Assert.ThrowsExactly<JsonException>(() =>
                Read<Holder>("{\"Shape\":{\"$type\":\"No.Such.Type, No.Such.Assembly\",\"$value\":{}}}"));
        }

        [TestMethod]
        public void Resolved_Type_Must_Fit_The_Declared_Type()
        {
            EnsureAliases();
            Assert.ThrowsExactly<JsonException>(() =>
                Read<Holder>("{\"Shape\":{\"$type\":\"envtest.item\",\"$value\":{\"Name\":\"x\"}}}"));
        }

        [TestMethod]
        public void References_Are_Preserved_Across_Envelopes()
        {
            EnsureAliases();
            var shared = new Item { Name = "shared" };
            var holder = new Holder
            {
                Anything = shared,
                Objects = new object[] { shared, new List<Item> { shared } },
                Map = new Dictionary<string, object> { ["x"] = shared }
            };

            var json = Write(holder, preserve: true);
            var back = Read<Holder>(json, preserve: true);

            StringAssert.Contains(json, "\"$ref\"");
            Assert.AreSame(back.Anything, back.Objects[0]);
            Assert.AreSame(back.Anything, ((List<Item>)back.Objects[1])[0]);
            Assert.AreSame(back.Anything, back.Map["x"]);
        }

        [TestMethod]
        public void Cycles_And_References_To_The_Outer_Root_Work()
        {
            EnsureAliases();
            var a = new Node { Name = "a" };
            var b = new Node { Name = "b", Next = a };
            a.Next = b;
            var backA = Read<Node>(Write(a, preserve: true), preserve: true);
            Assert.AreSame(backA, ((Node)backA.Next).Next);

            var root = new Node { Name = "root" };
            root.Next = new Child { Tag = "c", Parent = root };
            var backRoot = Read<Node>(Write(root, preserve: true), preserve: true);
            Assert.AreSame(backRoot, ((Child)backRoot.Next).Parent);
        }

        [TestMethod]
        public void Reference_To_An_Object_Of_Another_Type_Is_A_JsonException()
        {
            EnsureAliases();
            var json = "{\"Objects\":[{\"$type\":\"envtest.node\",\"$value\":{\"$id\":\"1\",\"Name\":\"n\"}}," +
                       "{\"$type\":\"envtest.item\",\"$value\":{\"$ref\":\"1\"}}]}";

            Assert.ThrowsExactly<JsonException>(() => Read<Holder>(json, preserve: true));
        }

        [TestMethod]
        public void Parallel_Serialization_Gives_Identical_Results_And_Isolated_References()
        {
            EnsureAliases();
            var shared = new Holder { Anything = new Item { Name = "same" }, Shape = new Circle { Radius = 1 } };
            var expected = Write(shared);
            var failures = 0;
            Parallel.For(0, 200, i =>
            {
                if (Write(shared) != expected)
                {
                    Interlocked.Increment(ref failures);
                }

                var own = new Item { Name = "p" + i };
                var back = Read<Holder>(Write(new Holder { Anything = own, Objects = new object[] { own } }, true), true);
                if (back.Anything is not Item { } item || item.Name != "p" + i || !ReferenceEquals(back.Anything, back.Objects[0]))
                {
                    Interlocked.Increment(ref failures);
                }
            });

            Assert.AreEqual(0, failures);
        }

        [TestMethod]
        public void Manual_Serializer_Goes_Through_The_Bridge()
        {
            EnsureAliases();
            var bag = new ManualBag { Values = { ["n"] = 3, ["s"] = "text", ["i"] = new Item { Name = "inner" } } };

            var json = JsonHelper.ToJson<object>(bag, Mode);
            var back = (ManualBag)Read<object>(json);

            StringAssert.Contains(json, "\"envtest.manual-bag\"");
            Assert.AreEqual<object>(3, back.Values["n"]);
            Assert.AreEqual<object>("text", back.Values["s"]);
            Assert.AreEqual("inner", ((Item)back.Values["i"]).Name);
            Assert.ThrowsExactly<JsonException>(() => JsonHelper.ToJson<object>(new DuplicateBag(), Mode));
        }

        [TestMethod]
        public void Shared_Manual_Serializer_Is_A_Reference()
        {
            EnsureAliases();
            var bag = new ManualBag { Values = { ["n"] = 1 } };

            var back = Read<object[]>(Write(new object[] { bag, bag }, preserve: true), preserve: true);

            Assert.AreSame(back[0], back[1]);
        }

        [TestMethod]
        public void Native_Polymorphism_Is_Left_To_System_Text_Json()
        {
            var back = Read<Zoo>(Write(new Zoo { Animal = new Dog { Name = "Rex", Barks = 3 } }));

            Assert.IsInstanceOfType<Dog>(back.Animal);
            Assert.AreEqual(3, ((Dog)back.Animal).Barks);
            var json = "{\"Animal\":{\"Name\":\"Rex\",\"Barks\":2,\"$type\":\"dog\"}}";
            Assert.AreEqual(2, ((Dog)Read<Zoo>(json).Animal).Barks, "discriminator not in first place");
        }

        [TestMethod]
        public void Assisted_Format_Is_Rejected()
        {
            // The shape the removed AssistedPolymorphism wrote.
            var legacy = "{\"$$Type\":\"SimpleContract\",\"Data\":[{\"PropertyName\":\"_\",\"TypeName\":\"envtest.item\",\"Data\":{\"Name\":\"x\"}}]}";

            Assert.ThrowsExactly<JsonException>(() => Read<Item>(legacy));
            Assert.ThrowsExactly<JsonException>(() => Read<Holder>($"{{\"Anything\":{legacy}}}"));
        }

        public class Item
        {
            public string Name { get; set; }
        }

        public abstract class Shape
        {
            public string Label { get; set; }
        }

        public class Circle : Shape
        {
            public double Radius { get; set; }
        }

        public class Square : Shape
        {
            public int Side { get; set; }
        }

        public class UnregisteredShape : Shape
        {
        }

        public class Unregistered
        {
            public int Value { get; set; }
        }

        public class Holder
        {
            public Shape Shape { get; set; }

            public object Anything { get; set; }

            public IComparable Comparable { get; set; }

            public List<Shape> Shapes { get; set; }

            public object[] Objects { get; set; }

            public Dictionary<string, object> Map { get; set; }
        }

        public class Node
        {
            public string Name { get; set; }

            public object Next { get; set; }
        }

        public class Child
        {
            public string Tag { get; set; }

            public Node Parent { get; set; }
        }

        [JsonPolymorphic]
        [JsonDerivedType(typeof(Dog), "dog")]
        public class Animal
        {
            public string Name { get; set; }
        }

        public class Dog : Animal
        {
            public int Barks { get; set; }
        }

        public class Zoo
        {
            public Animal Animal { get; set; }
        }

        /// <summary>An existing-style manual serializer, read and written through the bridge.</summary>
        public class ManualBag : IManualSerializer
        {
            public Dictionary<string, object> Values { get; set; } = new Dictionary<string, object>();

            public IList<ManualSerializationData> Data { get; set; }

            public void GetObjectData()
            {
                foreach (var pair in Values)
                {
                    Data.Add(ManualSerializationData.FromValue(pair.Key, pair.Value));
                }
            }

            public void ApplyObjectData()
            {
                Values = Data.ToDictionary(d => d.PropertyName, d => d.Data);
            }
        }

        /// <summary>Writes the same entry name twice - must fail loudly.</summary>
        public class DuplicateBag : IManualSerializer
        {
            public IList<ManualSerializationData> Data { get; set; }

            public void GetObjectData()
            {
                Data.Add(ManualSerializationData.FromValue("_", 1));
                Data.Add(ManualSerializationData.FromValue("_", 2));
            }

            public void ApplyObjectData()
            {
            }
        }
    }
}

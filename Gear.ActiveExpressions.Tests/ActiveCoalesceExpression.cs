using Gear.Components;
using NUnit.Framework;
using System.Collections.Concurrent;
using System.Linq;

namespace Gear.ActiveExpressions.Tests
{
    [TestFixture]
    class ActiveCoalesceExpression
    {
        [Test]
        public void ConsistentHashCode()
        {
            int hashCode1, hashCode2;
            var john = TestPerson.CreateJohn();
            using (var expr = ActiveExpression.Create(p1 => p1.Name ?? string.Empty, john))
                hashCode1 = expr.GetHashCode();
            using (var expr = ActiveExpression.Create(p1 => p1.Name ?? string.Empty, john))
                hashCode2 = expr.GetHashCode();
            Assert.IsTrue(hashCode1 == hashCode2);
        }

        [Test]
        public void Equality()
        {
            var john = TestPerson.CreateJohn();
            var emily = TestPerson.CreateEmily();
            using (var expr1 = ActiveExpression.Create(p1 => p1.Name ?? string.Empty, john))
            using (var expr2 = ActiveExpression.Create(p1 => p1.Name ?? string.Empty, john))
            using (var expr3 = ActiveExpression.Create(p1 => p1.Name ?? "Another String", john))
            using (var expr4 = ActiveExpression.Create(p1 => p1.Name ?? string.Empty, emily))
            {
                Assert.IsTrue(expr1 == expr2);
                Assert.IsFalse(expr1 == expr3);
                Assert.IsFalse(expr1 == expr4);
            }
        }

        [Test]
        public void Equals()
        {
            var john = TestPerson.CreateJohn();
            var emily = TestPerson.CreateEmily();
            using (var expr1 = ActiveExpression.Create(p1 => p1.Name ?? string.Empty, john))
            using (var expr2 = ActiveExpression.Create(p1 => p1.Name ?? string.Empty, john))
            using (var expr3 = ActiveExpression.Create(p1 => p1.Name ?? "Another String", john))
            using (var expr4 = ActiveExpression.Create(p1 => p1.Name ?? string.Empty, emily))
            {
                Assert.IsTrue(expr1.Equals(expr2));
                Assert.IsFalse(expr1.Equals(expr3));
                Assert.IsFalse(expr1.Equals(expr4));
            }
        }

        [Test]
        public void FaultPropagation()
        {
            var john = TestPerson.CreateJohn();
            var emily = TestPerson.CreateEmily();
            using (var expr = ActiveExpression.Create((p1, p2) => p1.Name.ToString() ?? p2.Name.ToString(), john, emily))
            {
                Assert.IsNull(expr.Fault);
                john.Name = null;
                Assert.IsNotNull(expr.Fault);
                emily.Name = null;
                john.Name = "John";
                Assert.IsNull(expr.Fault);
            }
        }

        [Test]
        public void FaultShortCircuiting()
        {
            var john = TestPerson.CreateJohn();
            using (var expr = ActiveExpression.Create<TestPerson, TestPerson, string>((p1, p2) => p1.Name ?? p2.Name, john, null))
            {
                Assert.AreEqual(john.Name, expr.Value);
                Assert.IsNull(expr.Fault);
            }
        }

        [Test]
        public void ImplicitConversion()
        {
            using (var expr = ActiveExpression.Create(() => new A() ?? new B()))
            {
                Assert.IsNull(expr.Fault);
                Assert.IsNull(expr.Value);
            }
        }

        #region ImplicitConversion Classes

        class A
        {
            public static implicit operator B(A a) => null;
        }

        class B
        {
        }

        #endregion ImplicitConversion Classes

        [Test]
        public void Inequality()
        {
            var john = TestPerson.CreateJohn();
            var emily = TestPerson.CreateEmily();
            using (var expr1 = ActiveExpression.Create(p1 => p1.Name ?? string.Empty, john))
            using (var expr2 = ActiveExpression.Create(p1 => p1.Name ?? string.Empty, john))
            using (var expr3 = ActiveExpression.Create(p1 => p1.Name ?? "Another String", john))
            using (var expr4 = ActiveExpression.Create(p1 => p1.Name ?? string.Empty, emily))
            {
                Assert.IsFalse(expr1 != expr2);
                Assert.IsTrue(expr1 != expr3);
                Assert.IsTrue(expr1 != expr4);
            }
        }

        [Test]
        public void PropertyChanges()
        {
            var john = TestPerson.CreateJohn();
            var emily = TestPerson.CreateEmily();
            var values = new BlockingCollection<string>();
            using (var expr = ActiveExpression.Create((p1, p2) => p1.Name ?? p2.Name, john, emily))
            {
                var disconnect = expr.OnPropertyChanged(ae => ae.Value, value => values.Add(value));
                values.Add(expr.Value);
                john.Name = "J";
                john.Name = "John";
                john.Name = null;
                emily.Name = "E";
                emily.Name = "Emily";
                emily.Name = null;
                emily.Name = "Emily";
                john.Name = "John";
                disconnect();
            }
            Assert.IsTrue(new string[] { "John", "J", "John", "Emily", "E", "Emily", null, "Emily", "John" }.SequenceEqual(values));
        }

        [Test]
        public void StringConversion()
        {
            var emily = TestPerson.CreateEmily();
            emily.Name = "X";
            using (var expr = ActiveExpression.Create(p1 => p1.Name ?? p1.Name.Length.ToString(), emily))
                Assert.AreEqual("({C} /* {X} */.Name /* \"X\" */ ?? {C} /* {X} */.Name /* \"X\" */.Length /* ? */.ToString() /* ? */) /* \"X\" */", expr.ToString());
        }

        [Test]
        public void ValueShortCircuiting()
        {
            var john = TestPerson.CreateJohn();
            var emily = TestPerson.CreateEmily();
            using (var expr = ActiveExpression.Create((p1, p2) => p1.Name ?? p2.Name, john, emily))
                Assert.AreEqual(john.Name, expr.Value);
            Assert.AreEqual(0, emily.NameGets);
        }
    }
}

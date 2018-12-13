using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace Gear.ActiveQuery.MSTest.Enumerable
{
    [TestClass]
    public class ToActiveEnumerable
    {
        [TestMethod]
        public void ActiveEnumerablesAreNotDisposed()
        {
            var people = TestPerson.CreatePeople();
            using (var query1 = people.ActiveWhere(p => p != null))
            {
                using (var query2 = query1.ToActiveEnumerable())
                    Assert.AreEqual(query1.Count, query2.Count);
                Assert.IsFalse(query1.IsDisposed);
            }
        }

        [TestMethod]
        public void WrappedObservableCollectionManipulation()
        {
            var people = TestPerson.CreatePeople();
            var wrappedPeople = new TestSimpleObservableCollection<TestPerson>(people.SynchronizationContext, people);
            using (var query = wrappedPeople.ToActiveEnumerable())
            {
                Assert.IsTrue(query.SequenceEqual(people));
                people.Add(new TestPerson("Jenny"));
                Assert.IsTrue(query.SequenceEqual(people));
                people.InsertRange(3, new TestPerson[] { new TestPerson("Renata"), new TestPerson("Tony") });
                Assert.IsTrue(query.SequenceEqual(people));
                people[4] = new TestPerson("Anthony");
                Assert.IsTrue(query.SequenceEqual(people));
                people.RemoveRange(3, 2);
                Assert.IsTrue(query.SequenceEqual(people));
                people.RemoveAt(people.Count - 1);
                Assert.IsTrue(query.SequenceEqual(people));
                people.Clear();
                Assert.IsTrue(query.SequenceEqual(people));
                Assert.IsTrue(query.IsSynchronized);
                wrappedPeople.IsSynchronized = false;
                Assert.IsFalse(query.IsSynchronized);
                wrappedPeople.IsSynchronized = true;
                Assert.IsTrue(query.IsSynchronized);
            }
        }
    }
}

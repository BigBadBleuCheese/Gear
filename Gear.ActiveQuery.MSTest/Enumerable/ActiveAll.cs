using Gear.Components;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace Gear.ActiveQuery.MSTest.Enumerable
{
    [TestClass]
    public class ActiveAll
    {
        [TestMethod]
        public void SourceManipulation()
        {
            var numbers = new RangeObservableCollection<int>(System.Linq.Enumerable.Range(1, 10).Select(i => i * 3));
            using (var query = numbers.ActiveAll(i => i % 3 == 0))
            {
                Assert.IsNull(query.OperationFault);
                Assert.IsTrue(query.Value);
                numbers[0] = 2;
                Assert.IsFalse(query.Value);
                numbers.RemoveAt(0);
                Assert.IsTrue(query.Value);
                --numbers[0];
                Assert.IsFalse(query.Value);
                numbers.Clear();
                Assert.IsTrue(query.Value);
                numbers.Add(7);
                Assert.IsFalse(query.Value);
            }
        }
    }
}

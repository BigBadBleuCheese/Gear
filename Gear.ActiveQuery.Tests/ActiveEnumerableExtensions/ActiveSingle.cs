using Gear.Components;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace Gear.ActiveQuery.Tests.ActiveEnumerableExtensions
{
    [TestClass]
    public class ActiveSingle
    {
        [TestMethod]
        public void EmptySource()
        {
            var numbers = new RangeObservableCollection<int>();
            using (var expr = numbers.ActiveSingle(i => i % 3 == 0))
            {
                Assert.IsNotNull(expr.OperationFault);
                Assert.AreEqual(0, expr.Value);
            }
        }

        [TestMethod]
        public void ExpressionlessEmptyNonNotifier()
        {
            var numbers = new int[0];
            using (var expr = numbers.ActiveSingle())
            {
                Assert.IsNotNull(expr.OperationFault);
                Assert.AreEqual(0, expr.Value);
            }
        }

        [TestMethod]
        public void ExpressionlessEmptySource()
        {
            var numbers = new RangeObservableCollection<int>();
            using (var expr = numbers.ActiveSingle())
            {
                Assert.IsNotNull(expr.OperationFault);
                Assert.AreEqual(0, expr.Value);
            }
        }

        [TestMethod]
        public void ExpressionlessMultiple()
        {
            var numbers = new RangeObservableCollection<int>(new int[] { 1, 1 });
            using (var expr = numbers.ActiveSingle())
            {
                Assert.IsNotNull(expr.OperationFault);
                Assert.AreEqual(0, expr.Value);
            }
        }

        [TestMethod]
        public void ExpressionlessNonNotifier()
        {
            var numbers = new int[] { 1 };
            using (var expr = numbers.ActiveSingle())
            {
                Assert.IsNull(expr.OperationFault);
                Assert.AreEqual(1, expr.Value);
            }
        }

        [TestMethod]
        public void ExpressionlessNonNotifierMultiple()
        {
            var numbers = new int[] { 1, 1 };
            using (var expr = numbers.ActiveSingle())
            {
                Assert.IsNotNull(expr.OperationFault);
                Assert.AreEqual(0, expr.Value);
            }
        }

        [TestMethod]
        public void ExpressionlessSourceManipulation()
        {
            var numbers = new RangeObservableCollection<int>(new int[] { 1 });
            using (var expr = numbers.ActiveSingle())
            {
                Assert.IsNull(expr.OperationFault);
                Assert.AreEqual(1, expr.Value);
                numbers.Add(2);
                Assert.IsNotNull(expr.OperationFault);
                Assert.AreEqual(0, expr.Value);
                numbers.RemoveAt(0);
                Assert.IsNull(expr.OperationFault);
                Assert.AreEqual(2, expr.Value);
                numbers.Clear();
                Assert.IsNotNull(expr.OperationFault);
                Assert.AreEqual(0, expr.Value);
            }
        }

        [TestMethod]
        public void Multiple()
        {
            var numbers = new RangeObservableCollection<int>(System.Linq.Enumerable.Range(1, 3).Select(i => i * 3));
            using (var expr = numbers.ActiveSingle(i => i % 3 == 0))
            {
                Assert.IsNotNull(expr.OperationFault);
                Assert.AreEqual(0, expr.Value);
            }
        }

        [TestMethod]
        public void SourceManipulation()
        {
            var numbers = new RangeObservableCollection<int>(System.Linq.Enumerable.Range(1, 3));
            using (var expr = numbers.ActiveSingle(i => i % 3 == 0))
            {
                Assert.IsNull(expr.OperationFault);
                Assert.AreEqual(3, expr.Value);
                numbers.RemoveAt(2);
                Assert.IsNotNull(expr.OperationFault);
                Assert.AreEqual(0, expr.Value);
                numbers.Add(3);
                Assert.IsNull(expr.OperationFault);
                Assert.AreEqual(3, expr.Value);
                numbers.Add(5);
                Assert.IsNull(expr.OperationFault);
                Assert.AreEqual(3, expr.Value);
                numbers.Add(6);
                Assert.IsNotNull(expr.OperationFault);
                Assert.AreEqual(0, expr.Value);
                numbers.Clear();
                Assert.IsNotNull(expr.OperationFault);
                Assert.AreEqual(0, expr.Value);
                numbers.Add(3);
                numbers.Add(6);
                Assert.IsNotNull(expr.OperationFault);
                Assert.AreEqual(0, expr.Value);
                numbers.RemoveAt(0);
                Assert.IsNull(expr.OperationFault);
                Assert.AreEqual(6, expr.Value);
            }
        }
    }
}

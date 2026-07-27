using System.Collections.Generic;
public class AOTGenericReferences : UnityEngine.MonoBehaviour
{

	// {{ AOT assemblies
	public static readonly IReadOnlyList<string> PatchedAOTAssemblyList = new List<string>
	{
		"System.Core.dll",
		"UnityEngine.CoreModule.dll",
		"UnityEngine.JSONSerializeModule.dll",
		"mscorlib.dll",
	};
	// }}

	// {{ constraint implement type
	// }} 

	// {{ AOT generic types
	// System.Action<object>
	// System.Collections.Generic.ArraySortHelper<object>
	// System.Collections.Generic.Comparer<int>
	// System.Collections.Generic.Comparer<object>
	// System.Collections.Generic.Dictionary.Enumerator<object,object>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<object,object>
	// System.Collections.Generic.Dictionary.KeyCollection<object,object>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<object,object>
	// System.Collections.Generic.Dictionary.ValueCollection<object,object>
	// System.Collections.Generic.Dictionary<object,object>
	// System.Collections.Generic.EqualityComparer<object>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<object,object>>
	// System.Collections.Generic.ICollection<object>
	// System.Collections.Generic.IComparer<int>
	// System.Collections.Generic.IComparer<object>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<object,object>>
	// System.Collections.Generic.IEnumerable<float>
	// System.Collections.Generic.IEnumerable<object>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<object,object>>
	// System.Collections.Generic.IEnumerator<float>
	// System.Collections.Generic.IEnumerator<object>
	// System.Collections.Generic.IEqualityComparer<object>
	// System.Collections.Generic.IList<object>
	// System.Collections.Generic.KeyValuePair<object,object>
	// System.Collections.Generic.List.Enumerator<object>
	// System.Collections.Generic.List<object>
	// System.Collections.Generic.ObjectComparer<int>
	// System.Collections.Generic.ObjectComparer<object>
	// System.Collections.Generic.ObjectEqualityComparer<object>
	// System.Collections.ObjectModel.ReadOnlyCollection<object>
	// System.Comparison<object>
	// System.Func<float,byte>
	// System.Func<object,byte>
	// System.Func<object,float>
	// System.Func<object,int,object>
	// System.Func<object,int>
	// System.Linq.Buffer<object>
	// System.Linq.Enumerable.<ReverseIterator>d__79<object>
	// System.Linq.Enumerable.<SelectIterator>d__5<object,object>
	// System.Linq.Enumerable.<SkipIterator>d__31<object>
	// System.Linq.Enumerable.Iterator<float>
	// System.Linq.Enumerable.Iterator<object>
	// System.Linq.Enumerable.WhereArrayIterator<object>
	// System.Linq.Enumerable.WhereEnumerableIterator<float>
	// System.Linq.Enumerable.WhereEnumerableIterator<object>
	// System.Linq.Enumerable.WhereListIterator<object>
	// System.Linq.Enumerable.WhereSelectArrayIterator<object,float>
	// System.Linq.Enumerable.WhereSelectEnumerableIterator<object,float>
	// System.Linq.Enumerable.WhereSelectListIterator<object,float>
	// System.Linq.EnumerableSorter<object,int>
	// System.Linq.EnumerableSorter<object>
	// System.Linq.OrderedEnumerable.<GetEnumerator>d__1<object>
	// System.Linq.OrderedEnumerable<object,int>
	// System.Linq.OrderedEnumerable<object>
	// System.Predicate<object>
	// }}

	public void RefMethods()
	{
		// object[] System.Array.Empty<object>()
		// bool System.Linq.Enumerable.All<object>(System.Collections.Generic.IEnumerable<object>,System.Func<object,bool>)
		// bool System.Linq.Enumerable.Any<object>(System.Collections.Generic.IEnumerable<object>,System.Func<object,bool>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.AsEnumerable<object>(System.Collections.Generic.IEnumerable<object>)
		// float System.Linq.Enumerable.Average<object>(System.Collections.Generic.IEnumerable<object>,System.Func<object,float>)
		// int System.Linq.Enumerable.Count<object>(System.Collections.Generic.IEnumerable<object>,System.Func<object,bool>)
		// object System.Linq.Enumerable.FirstOrDefault<object>(System.Collections.Generic.IEnumerable<object>,System.Func<object,bool>)
		// System.Linq.IOrderedEnumerable<object> System.Linq.Enumerable.OrderBy<object,int>(System.Collections.Generic.IEnumerable<object>,System.Func<object,int>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Reverse<object>(System.Collections.Generic.IEnumerable<object>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.ReverseIterator<object>(System.Collections.Generic.IEnumerable<object>)
		// System.Collections.Generic.IEnumerable<float> System.Linq.Enumerable.Select<object,float>(System.Collections.Generic.IEnumerable<object>,System.Func<object,float>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Select<object,object>(System.Collections.Generic.IEnumerable<object>,System.Func<object,int,object>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.SelectIterator<object,object>(System.Collections.Generic.IEnumerable<object>,System.Func<object,int,object>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Skip<object>(System.Collections.Generic.IEnumerable<object>,int)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.SkipIterator<object>(System.Collections.Generic.IEnumerable<object>,int)
		// object[] System.Linq.Enumerable.ToArray<object>(System.Collections.Generic.IEnumerable<object>)
		// System.Collections.Generic.List<object> System.Linq.Enumerable.ToList<object>(System.Collections.Generic.IEnumerable<object>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Where<object>(System.Collections.Generic.IEnumerable<object>,System.Func<object,bool>)
		// System.Collections.Generic.IEnumerable<float> System.Linq.Enumerable.Iterator<object>.Select<float>(System.Func<object,float>)
		// object UnityEngine.GameObject.AddComponent<object>()
		// object UnityEngine.JsonUtility.FromJson<object>(string)
	}
}
using Inventory.Shared;

namespace Inventory.Service
{
    public interface ISortTable<T>
    {
        List<T> SortItem(List<T> list, string column);    

    }
}

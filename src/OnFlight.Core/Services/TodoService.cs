using Microsoft.Extensions.Logging;
using OnFlight.Contracts.Enums;
using OnFlight.Core.Data;
using OnFlight.Core.Data.Repositories;
using OnFlight.Core.Models;

namespace OnFlight.Core.Services;

public class TodoService : ITodoService
{
    private readonly ITodoListRepository _listRepo;
    private readonly ITodoItemRepository _itemRepo;
    private readonly DbConnectionFactory _dbFactory;
    private readonly ILogger<TodoService> _logger;

    public TodoService(ITodoListRepository listRepo, ITodoItemRepository itemRepo,
        DbConnectionFactory dbFactory, ILogger<TodoService> logger)
    {
        _listRepo = listRepo;
        _itemRepo = itemRepo;
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task<TodoList> CreateListAsync(string name, Guid? parentItemId = null)
    {
        var list = new TodoList { Name = name, ParentItemId = parentItemId };
        await _listRepo.InsertAsync(list);
        _logger.LogInformation("Created list {ListName} ({ListId})", list.Name, list.Id);
        return list;
    }

    public async Task<TodoList?> GetListAsync(Guid listId)
    {
        var list = await _listRepo.GetByIdAsync(listId);
        if (list != null)
            list.Items = (await _itemRepo.GetByListIdAsync(listId)).ToList();
        return list;
    }

    public Task<IEnumerable<TodoList>> GetRootListsAsync() => _listRepo.GetRootListsAsync();

    public async Task UpdateListAsync(TodoList list)
    {
        await _listRepo.UpdateAsync(list);
    }

    public async Task DeleteListAsync(Guid listId)
    {
        var (conn, tx) = _dbFactory.CreateTransaction();
        try
        {
            await _itemRepo.SoftDeleteByListIdAsync(listId, conn, tx);
            await _listRepo.SoftDeleteAsync(listId, conn, tx);
            tx.Commit();
            _logger.LogInformation("Deleted list {ListId}", listId);
        }
        catch (Exception ex)
        {
            tx.Rollback();
            _logger.LogError(ex, "Failed to delete list {ListId}, transaction rolled back", listId);
            throw;
        }
        finally
        {
            tx.Dispose();
            conn.Dispose();
        }
    }

    public async Task<TodoItem> AddItemAsync(Guid listId, string title, FlowNodeType nodeType = FlowNodeType.Task)
    {
        var maxSort = await _itemRepo.GetMaxSortOrderAsync(listId);
        var item = new TodoItem
        {
            Title = title,
            ParentListId = listId,
            SortOrder = maxSort + 1,
            NodeType = nodeType
        };
        await _itemRepo.InsertAsync(item);
        _logger.LogInformation("Added item {Title} to list {ListId}", title, listId);
        return item;
    }

    public Task<TodoItem?> GetItemAsync(Guid itemId) => _itemRepo.GetByIdAsync(itemId);

    public async Task UpdateItemAsync(TodoItem item)
    {
        await _itemRepo.UpdateAsync(item);
    }

    public async Task DeleteItemAsync(Guid itemId)
    {
        var item = await _itemRepo.GetByIdAsync(itemId);
        if (item == null) return;

        var (conn, tx) = _dbFactory.CreateTransaction();
        try
        {
            if (item.SubListId.HasValue)
            {
                await _itemRepo.SoftDeleteByListIdAsync(item.SubListId.Value, conn, tx);
                await _listRepo.SoftDeleteAsync(item.SubListId.Value, conn, tx);
            }

            if (item.NodeType == FlowNodeType.Fork)
                await RemoveForkFromJoinsAsync(item.ParentListId, itemId, conn, tx);

            await _itemRepo.SoftDeleteAsync(itemId, conn, tx);
            tx.Commit();
            _logger.LogInformation("Deleted item {ItemId}", itemId);
        }
        catch (Exception ex)
        {
            tx.Rollback();
            _logger.LogError(ex, "Failed to delete item {ItemId}, transaction rolled back", itemId);
            throw;
        }
        finally
        {
            tx.Dispose();
            conn.Dispose();
        }
    }

    private async Task RemoveForkFromJoinsAsync(Guid listId, Guid deletedForkId,
        System.Data.IDbConnection? conn = null, System.Data.IDbTransaction? tx = null)
    {
        var siblings = await _itemRepo.GetByListIdAsync(listId);
        foreach (var sibling in siblings.Where(s => s.NodeType == FlowNodeType.Join && !s.IsDeleted))
        {
            if (string.IsNullOrEmpty(sibling.FlowConfigJson)) continue;

            var modified = false;
            try
            {
                var config = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(sibling.FlowConfigJson);
                if (config == null) continue;

                var dict = new Dictionary<string, object>();
                foreach (var kv in config)
                {
                    if (kv.Key == "forkItemIds" && kv.Value.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        var remaining = kv.Value.EnumerateArray()
                            .Select(e => e.GetString())
                            .Where(s => s != null && s != deletedForkId.ToString())
                            .ToList();
                        if (remaining.Count < kv.Value.GetArrayLength())
                            modified = true;
                        if (remaining.Count > 0)
                            dict["forkItemIds"] = remaining;
                    }
                    else if (kv.Key == "forkItemId")
                    {
                        var val = kv.Value.GetString();
                        if (val == deletedForkId.ToString())
                            modified = true;
                        else if (val != null)
                            dict["forkItemIds"] = new List<string> { val };
                    }
                    else
                    {
                        dict[kv.Key] = kv.Value;
                    }
                }

                if (modified)
                {
                    sibling.FlowConfigJson = dict.Count > 0
                        ? System.Text.Json.JsonSerializer.Serialize(dict) : null;
                    await _itemRepo.UpdateAsync(sibling, conn, tx);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update Join node FlowConfig for sibling {SiblingId} while removing Fork {ForkId}", sibling.Id, deletedForkId);
            }
        }
    }

    public async Task<bool> WouldCreateCycleAsync(Guid sourceListId, Guid targetListId)
    {
        if (sourceListId == targetListId) return true;

        var visited = new HashSet<Guid> { sourceListId };
        return await HasPathToAsync(targetListId, sourceListId, visited);
    }

    private async Task<bool> HasPathToAsync(Guid fromListId, Guid toListId, HashSet<Guid> visited)
    {
        if (fromListId == toListId) return true;
        if (!visited.Add(fromListId)) return false;

        var list = await _listRepo.GetByIdAsync(fromListId);
        if (list == null) return false;

        var items = (await _itemRepo.GetByListIdAsync(fromListId))
            .Where(i => !i.IsDeleted && i.NodeType == FlowNodeType.Fork)
            .ToList();

        foreach (var item in items)
        {
            var forkTargetId = ParseTargetListId(item.FlowConfigJson);
            if (forkTargetId == null) continue;
            if (await HasPathToAsync(forkTargetId.Value, toListId, visited))
                return true;
        }

        return false;
    }

    private static Guid? ParseTargetListId(string? json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            var config = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(json);
            if (config != null && config.TryGetValue("targetListId", out var el))
                return Guid.Parse(el.GetString()!);
        }
        catch { }
        return null;
    }

    public async Task SetItemStatusAsync(Guid itemId, TodoStatus status)
    {
        var item = await _itemRepo.GetByIdAsync(itemId);
        if (item == null) return;
        item.Status = status;
        await _itemRepo.UpdateAsync(item);
    }

    public async Task ReorderItemsAsync(Guid listId, List<Guid> orderedIds)
    {
        var items = orderedIds.Select((id, i) => (id, order: i)).ToList();
        await _itemRepo.BatchUpdateSortOrderAsync(listId, items);
    }

    public async Task<TodoList> CreateSubListAsync(Guid parentItemId, string name)
    {
        var parentItem = await _itemRepo.GetByIdAsync(parentItemId);
        if (parentItem == null) throw new InvalidOperationException("Parent item not found");

        var (conn, tx) = _dbFactory.CreateTransaction();
        try
        {
            var subList = new TodoList { Name = name, ParentItemId = parentItemId };
            await _listRepo.InsertAsync(subList, conn, tx);

            parentItem.SubListId = subList.Id;
            await _itemRepo.UpdateAsync(parentItem, conn, tx);

            tx.Commit();
            _logger.LogInformation("Created sub-list {SubListName} under item {ParentItemId}", name, parentItemId);
            return subList;
        }
        catch (Exception ex)
        {
            tx.Rollback();
            _logger.LogError(ex, "Failed to create sub-list for item {ParentItemId}, transaction rolled back", parentItemId);
            throw;
        }
        finally
        {
            tx.Dispose();
            conn.Dispose();
        }
    }
}

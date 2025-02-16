﻿using Microsoft.EntityFrameworkCore;

namespace Domain.DTOs.Paging;

public class BasePaging<T>
{
    public BasePaging()
    {
        Page = 1;
        TakeEntity = 12;
        HowManyShowPageAfterAndBefore = 5;
        Entities = new List<T>();
    }

    public int Page { get; set; }

    public int PageCount { get; set; }

    public int AllEntitiesCount { get; set; }

    public int StartPage { get; set; }

    public int EndPage { get; set; }

    public int TakeEntity { get; set; }

    public int SkipEntity { get; set; }

    public int HowManyShowPageAfterAndBefore { get; set; }
    public int ShowPageCountId { get; set; }

    public List<T> Entities { get; set; }

    public PagingViewModel GetCurrentPaging()
    {
        return new PagingViewModel
        {
            EndPage = EndPage,
            Page = Page,
            StartPage = StartPage
        };
    }

    public string GetShownEntitiesPagesTitle()
    {
        if (AllEntitiesCount != 0)
        {
            var startItem = 1;
            var endItem = AllEntitiesCount;

            if (EndPage > 1)
            {
                startItem = (Page - 1) * TakeEntity + 1;
                endItem = Page * TakeEntity > AllEntitiesCount ? AllEntitiesCount : Page * TakeEntity;
            }

            return $"نمایش {startItem} تا {endItem} از {AllEntitiesCount}";
        }

        return "0 آیتم";
    }

    public async Task<BasePaging<T>> Paging(IQueryable<T> queryable)
    {
        if (Page < 1) Page = 1;
        var allEntitiesCount = await queryable.AsQueryable().CountAsync();
        if (allEntitiesCount < TakeEntity) Page = 1;
        var showPageCountId = allEntitiesCount - (Page - 1) * TakeEntity;

        var pageCount = TakeEntity == 0 ? 1 : Convert.ToInt32(Math.Ceiling(allEntitiesCount / (double)TakeEntity));

        ShowPageCountId = showPageCountId;
        AllEntitiesCount = allEntitiesCount;
        SkipEntity = (Page - 1) * TakeEntity;
        StartPage = Page - HowManyShowPageAfterAndBefore <= 0 ? 1 : Page - HowManyShowPageAfterAndBefore;
        EndPage = Page + HowManyShowPageAfterAndBefore > pageCount ? pageCount : Page + HowManyShowPageAfterAndBefore;
        PageCount = pageCount;
        
        if (TakeEntity == 0)
        {
            SkipEntity = 0;
            TakeEntity = allEntitiesCount;
        }
        Entities = await queryable.Skip(SkipEntity).Take(TakeEntity).ToListAsync();
        return this;
    }

    public async Task<BasePaging<T>> Paging(IEnumerable<T> enumerable)
    {
        if (Page < 1) Page = 1;
        var allEntitiesCount = enumerable.Count();
        if (allEntitiesCount < TakeEntity) Page = 1;
        var showPageCountId = allEntitiesCount - (Page - 1) * TakeEntity;

        var pageCount = Convert.ToInt32(Math.Ceiling(allEntitiesCount / (double)TakeEntity));
        
        ShowPageCountId = showPageCountId;
        AllEntitiesCount = allEntitiesCount;
        SkipEntity = (Page - 1) * TakeEntity;
        StartPage = Page - HowManyShowPageAfterAndBefore <= 0 ? 1 : Page - HowManyShowPageAfterAndBefore;
        EndPage = Page + HowManyShowPageAfterAndBefore > pageCount ? pageCount : Page + HowManyShowPageAfterAndBefore;
        PageCount = pageCount;

        if (TakeEntity == 0)
        {
            SkipEntity = 0;
            TakeEntity = allEntitiesCount;
        }

        Entities = enumerable.Skip(SkipEntity).Take(TakeEntity).ToList();
        await Task.CompletedTask;

        return this;
    }
}

public class PagingViewModel
{
    public int Page { get; set; }

    public int StartPage { get; set; }

    public int EndPage { get; set; }
}
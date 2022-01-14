using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Diz.Core.Interfaces;

namespace Diz.Core.model;

public class DataStoreProvider<T> : IDataStoreProvider<T> where T : class
{
    public List<T> Items { get; set; } = new();

    public bool AddIfDoesntExist(T type)
    {
        if (Items.Exists(x => x.GetType() == type.GetType()))
            return false;
        
        Items.Add(type);
        return true;
    }

    public TSearchFor Get<TSearchFor>() where TSearchFor : class, T
    {
        try
        {
            return Items.Single(x => x is TSearchFor) as TSearchFor;
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidOperationException($"No API found of type {typeof(T).Name}", ex);
        }
    }

    public IEnumerator<T> GetEnumerator() => Items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)Items).GetEnumerator();
}
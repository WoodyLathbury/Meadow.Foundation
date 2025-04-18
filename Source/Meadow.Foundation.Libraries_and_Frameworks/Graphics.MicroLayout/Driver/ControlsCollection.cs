﻿using System;
using System.Collections;
using System.Collections.Generic;

namespace Meadow.Foundation.Graphics.MicroLayout;

internal interface IControlContainer : IControl
{
    ControlsCollection Controls { get; }
}

/// <summary>
/// Represents a collection of display controls on a <see cref="DisplayScreen"/>.
/// </summary>
public sealed class ControlsCollection : IEnumerable<IControl>
{
    private readonly List<IControl> _controls = new();
    private readonly object _syncRoot = new();

    private readonly IControlContainer? _container;

    /// <summary>
    /// Initializes a new instance of the <see cref="ControlsCollection"/> class.
    /// </summary>
    /// <param name="parent">The parent control (if exists)</param>
    internal ControlsCollection(IControlContainer? parent)
    {
        _container = parent;
    }

    internal object SyncRoot => _syncRoot;

    /// <summary>
    /// Gets a control from the Controls collection by index
    /// </summary>
    /// <param name="index">index of the control to retrieve</param>
    public IControl this[int index]
    {
        get
        {
            if (index < 0 || index >= _controls.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            return _controls[index];
        }
    }

    /// <summary>
    /// Removes all display controls from the collection.
    /// </summary>
    public void Clear()
    {
        lock (SyncRoot)
        {
            _controls.Clear();
            _container?.Parent?.Invalidate();
        }
    }

    /// <summary>
    /// Gets the number of display controls in the collection.
    /// </summary>
    public int Count => _controls.Count;

    /// <summary>
    /// Adds one or more display controls to the collection.
    /// </summary>
    /// <param name="controls">The display controls to add.</param>
    public void Add(params IControl[] controls)
    {
        // Apply screen theme to the added controls, if available.
        if (_container is DisplayScreen screen)
        {
            if (screen.Theme != null)
            {
                foreach (IThemedControl control in controls)
                {
                    control.ApplyTheme(screen.Theme);
                }
            }
        }

        lock (SyncRoot)
        {
            foreach (var control in controls)
            {
                if (control is null) { continue; }

                control.Parent = _container;
                control.Invalidate();
                _controls.Add(control);
            }
        }
    }

    /// <summary>
    /// Removes a control from the collection.
    /// </summary>
    /// <param name="control">The control to remove.</param>
    /// <returns>True if the control was removed; otherwise, false.</returns>
    public bool Remove(IControl control)
    {
        if (control is null) { return false; }

        lock (SyncRoot)
        {
            if (_controls.Remove(control))
            {
                _container?.Parent?.Invalidate();
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns an enumerator that iterates through the collection of display controls.
    /// </summary>
    /// <returns>An enumerator that can be used to iterate through the collection.</returns>
    public IEnumerator<IControl> GetEnumerator()
    {
        return _controls.GetEnumerator();
    }

    /// <summary>
    /// Returns an enumerator that iterates through the collection of display controls.
    /// </summary>
    /// <returns>An enumerator that can be used to iterate through the collection.</returns>
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
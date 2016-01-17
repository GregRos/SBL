# SimpleBindingLibrary

SBL is a .NET library designed to create something similar to WPF-style data-binding in environments that don't use WPF. It's meant to be portable and lightweight, and be usable for UI design or just arbitrary objects.

The SBL design is very different from WPF, however. It revolves around workflow-like composable objects called `Bindables` that project data into different forms and link some bits of data with other bits.

Loosely speaking, it tries to uphold the following principles:

1. Strong typing.
2. Language-friendly API (e.g. lots of syntax sugar)
3. Extensibility.
4. Ease of debugging/diagnostics
5. Thread safety, naturally (a tricky bugger) 
5. Other things

The overall design is still being worked on.

After some experimentation, I've decided to write it in F#. However, it will be heavily focused towards C# and other CLI languages, though it will also have some F#-specific features.

## Bindables
Bindables are the central building block of SBL. They serve as points for SBL data binding. Bindables implement `IBindable<T>`, `IBindable`, and possibly other interfaces.

These are workflow-like objects that expose a single value (possibly a collection or a tuple) and provide a number of features surrounding that value:

1. Data binding 
2. Validation
3. Exception handling
4. Metadata store
5. Change notification

Bindables also support deterministic disposal via `IDisposable`. You'll see why this is needed in the future.

Bindables either provide a value source, or else are *linked* to another bindable and augment it, such as by projecting its value into a different form. 

Bindables are linked into long chains that may be explored in runtime. Chains of bindables are immutable, though some things about the bindables themselves are be modifiable.

### Example

Here is an example of such a chain (C#):

	var source = 
		 Bindable.Var(0) //value source
		.Convert(convertOut: x => (long)x, convertIn: x => (int)x)
		.Dispatch(dispatcher);
		
This chain may be printed as follows:

	VariableBindable <|=> ConvertingBindable <|=> DispatchingBindable
	
`A <|=> B` means that `B` is dependent on `A` for its definition. These kinds of links can also be one-directional, such as `A |=> B`, means that data only goes from `A` to `B` and not the other way around.

1.  `VariableBindable` is backed by an internal variable (or rather, a field).

2.  It is linked to a `ConvertingBindable`, which applies a conversion on its value, what in WPF would be provided by a `Converter` object. It exposes the new, converted value, which may be of a different type. Note that both input and output converters are provided to enable both reading and writing.

3. This is in turn linked to a `DispatchingBindable`, whose purpose is to funnel all reads/writes through a dispatcher, as is required by windows forms when working with multiple threads.

## Bindings
A binding is an object that synchronizes the values of two bindables. Basically, they join two different chains such as in the above example and mediate between them in different ways.

In a binding, each bindable is a separate and independent entity. 

Bindings are written, `A <|-> B`. `A` is called the target and `B` is called the source. Bindings are always between exactly two bindables. If multiple values are required, another bindable should be inserted to provide additional processing.

A binding has a direction which indicates how changes propagate. This isn't related to what is the source and what is the target. 

To set a binding of a bindable, you set its `Binding` property to a `Binding<T>` instance wrapping a target bindable. For example:
	
	Target.Binding = Source.ToBinding(BindingMode.TwoWay)
	
Bindings are disposable. When a binding is disposed, the two bindables are no longer synchronized. In addition, the source bindable's `Binding` property is reset to `null`. 

When either bindable participating in the binding is disposed, the binding is disposed as well. However, unlike linked bindables, the disposal of one bindable does not cause the disposal of the other.

Bindings are very light objects in comparison to WPF. Most of the heavy lifting is done by the bindables.

## Metadata
Bindables support metadata. This allows you to attach arbitrary information to a bindable. Most metadata is not used by the library, with a few important exceptions (such as `Owner` and `Name`).

Basically, each bindable carries with it a mutable `object -> object` dictionary that can be accessed at any time.

### Owner
The owner is a special piece of metadata that actually does something special.

SBL allows you to attach bindables to other objects (for example, you can attach a bindable backed by an object's property to that object). This is used for diagnostics and informational purposes.

When you set the `Owner` metadata, the bindable on which you set it is registered in a special dictionary. You can later look that bindable up through that dictionary, using its owner object as a key.

### Name
This piece of metadata may be used by some diagnostics or informational functions. It is a string.

## Validation
Bindables support validation. Validation allows for a bindable to have a value which isn't exceptional, in that it shouldn't disrupt the flow of the program, but is still invalid. 

Bindables expose the `IsValid` property, which is itself a bindable and thus can be bound to (incidentally, it also has an `IsValid` member, and so on, but all of these are always true). This allows you to bind the result a control, turning it background red when validation fails.

Validation is achieved through *validation rules*, which are just `IBindable<bool>` objects that are maintained by the `IsValid` bindable. They are usually also *linked* to the bindable they validate, but in principle this is not required. 

Additional information about the rule (such as an error text) can be provided by attaching metadata to the bindable.

To pass validation, all rules must return `true`. Failed rules are exposed through the `IsValid` property.

If `A <|=> B`, then `B` inherits the validation rules of `A`, and if `A` fails validation with a set of rules, then `B` will also report those failures.

Because of this, the `IsValid` property exposes `FailureInformation` objects that give both the rule that failed, and the bindable where it failed.

## Change Notification
Bindables support change notification. This has two different parts:

1. They notify observers when a change occurs.
2. They can be notified (or else they detect) that a change has occurred.

Bindables expose a `NotifyChange` member that lets you notify the bindable that a change has occurred. This is important because it's only possible to detect change in the data source externally.

They also expose a `Changed` event that is raised when a change occurs. 

### ContextualChangeInfo

The `Change` event (and its `NotifyChange` invocator) supports an additional argument of type `ContextualChangeInfo`, which provides additional information about the change.

The argument usually provides incremental information about how the change occurred, such as the information exposed by `INotifyCollectionChanged`.

It is important to note that this extra parameter should be used for performance, diagnostics, and similar reasons. It should not contain information not exposed by the `Value` property.

When it is provided, this argument should be completely valid, but it doesn't have to be complete and it may not even be provided.

### NotificationModes
These are components that can detect when a change has occurred and notify the bindable automatically. Here are some examples:

1. `EventRaised` notification mode, which listens for an event on the owner or another object and notifies change of the bindable when the event has been raised. Optionally, you can specify a filter so notification is only raised on certain conditions. Optionally, you can provide an alternative target to monitor (instead of the owner).
2. `PropertyChanged` notification mode, which is a specialized form of the previous mode.
3. `ListenBindable` notification mode, which is also a specialized form of `EventRaised`. Monitors the specified bindable and reports change when it has occurred.

When you add a notification mode to a bindable, it is registered with the bindable. You can get a list of all modes active on it, and remove them.

## Exception Raising/Handling
The library should support exception raising and handling, but I'm not sure how exactly yet.

Because bindable chains can become extremely complex, it's important to identify and log where the exception occurred. As such, exceptions should provide significantly more information than normal, including a binding trace, location of the exception, what SBL was doing when it was thrown, etc.

It is certain however that exceptions will not be swallowed like they are in WPF, although that could be an option. By default they will be thrown. And they are not meant to indicate validation issues.


## Additional Info

### About Links
Links between bindables have a few noteworthy characteristics. If `A <|=> B` then:

1. Each bindable can be the source of any number of links, and establishing links doesn't change the source bindable. So for example the following is fine:

		var value = Bindable.Var(0);
		var value1 = value.Convert(x => x.ToString());
		var value2 = value.Convert(x => (long)x);
	
	And is common.
	
2. If `A` is disposed, then `B` is disposed as well.

3. Due to change notification, `B` is reachable from `A` and is thus cannot be garbage collected. However, if `B` is disposed, it stops monitoring `A` and can be garbage collected.

4. The link is meant to be lazy in the following way:
	If `A` changes, `B` is not meant to be updated until required (generally, when a binding or external action tries to get or set `B`'s value). So for example a converter should not be called until someone invokes its `Value` member, and then the result should be cached.
	 However, while this requirement is great due to all sorts of reasons, it will be not be implemented at first because it is hard to achieve.

5. Values are still propogated between `A` and `B` even if one of them fails validation, unlike in a binding (see below). However, `B` inherits any validation rules on `A`.

6. `A` can be accessed from `B` using the `Parent` property. 

8. If a bindable has more than one source (e.g. similar to a WPF `MultiBinding`), its `Parent` will probably contain some kind of special object.

## Example Bindables
Not all are implemented. 
### ComputedBindable[T]
Backed by a function of the form `() => T`, which is used to generate the value. The value is cached and is regenerated only when `NotifyChange` is called.

This bindable is read-only.

### ConvertBindable[TIn, TOut]
Wraps another bindable and projects its value backwards and forwards using a converter you specify. 

This bindable is not suited for collections.

### ConstBindable[T]
Backed by a constant value of type `T`. Read only.

### VarBindable[T]
Backed by a variable value of type `T`.

### ListBindable[T]
Backed by `IList<T>` instance. The reference to the list never changes. When the bindable is set, the backing list is synchronized with the input list.

The list instance is generally required to implement `INotifyCollectionChanged` or similar for proper operation.

### SelectBindable[TIn, TOut]
Linked to an `IListBindable<TIn>` instance and projects its elements forwards and backwards using a pair of converters you supply.

You can supply only one converter, but in this case the collection will not be readable/writable.

### WhereBindable[T]
Linked to an `IListBindable<T>` instance and filters the collection based on a predicate you supply. The filtering is real-time, and the exposed, filtered collection is synchronized with the backing collection.

This bindable is *not* read-only, but writing to it may result in strange behavior.

### AggregateBindable[TElem, TResult]
Linked to an `IListBindable<T>` instance and applies a user-specified accumulator on the elements of the list, similar to the `Aggregate` extension method.

Unlike `ConverterBindable`, this bindable is optimized for collections. You can provide a set of operations that are used in order to update the aggregate result whenever the collection changes, so it doesn't have to be fully recalculated with every change.

Read-only.

### DisposingListBindable[T]
This bindable wraps a `IListBindable<T>` and disposes of `IDisposable` list elements when they are removed from the underlying collection.

This bindable is useful when you want to project a list of objects to a list of controls, and you want the controls disposed when their source objects are removed.
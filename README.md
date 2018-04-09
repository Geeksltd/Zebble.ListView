[logo]: https://raw.githubusercontent.com/Geeksltd/Zebble.ListView/master/Shared/NuGet/Icon.png "Zebble.ListView"


## Zebble.ListView

![logo]

A Zebble plugin to allows you to render a data source vertically based on a Template class.


[![NuGet](https://img.shields.io/nuget/v/Zebble.ListView.svg?label=NuGet)](https://www.nuget.org/packages/Zebble.ListView/)

> With this plugin if you have a data source (e.g. List<Customer>) you can then define a template class as a view sub-class (e.g. CustomerRowItem) that will take an item as input (e.g. Customer) and visualise that using other sub-views.

<br>


### Setup
* Available on NuGet: [https://www.nuget.org/packages/Zebble.ListView/](https://www.nuget.org/packages/Zebble.ListView/)
* Install in your platform client projects.
* Available for iOS, Android and UWP.
<br>


### Api Usage

If you want to show the user's contact list, you should create a ListView that the z-base is ListViewItem[Contact]:
```xml
<z-place inside="Body">
    <ListView z-of="Contact, Row"  DataSource="Items," Id="List" >
      <z-Component z-type="Row"  z-base="ListViewItem[Contact]" >
        <Stack>
          <TextView Id="Name" Text="@Item.Name" />
        </Stack>
      </z-Component>
    </ListView>
  </z-place>
```
```csharp
partial class ListViewSample
{
    public List<Contact> Items;
    
	public override async Task OnInitializing()
    {
        Items = GetSource().ToList();
        await base.OnInitializing();
        await InitializeComponents();
    }

    IEnumerable<Contact> GetSource() => Database.GetList<Contact>();
    public async Task ReloadButtonTapped() => await Reload();
    public async Task Reload() => await List.UpdateSource(Items = GetSource().ToList());

	}
}
```
<br>

#### Pull to Refresh

The Page class has a method named OnRefreshRequested which accepts a Func<Task> argument as a handler. It enables the pull to refresh effect on the page's BodyScroller (which is expected to be a ScrollView). When the user scrolls to the top, and then some, then the provided method will be invoked.

Page Markup
To use pull to refresh, you need to set EnablePullToRefresh on the page to true. Then upon showing of the page, it will look for a scroll view component  on the page and configure it. If there is no scroll view on the page, it will log an error on the Visual Studio output window and exit.

```xml
<z-Component z-type="MyPage" .... EnablePullToRefresh="true">
    ...
</z-Component>
```
```csharp
// In the code behind class of where your ListView instance is defined.
...
// Pull the latest source
public async Task Reload()
{
     Items = await GetSource(); // Usually from a remote data source such as a Web Api.
     await MyListView.UpdateSource(Items);
}
...
```
All that is remaining is to make the Reload() method called when the user pulls to refresh:
```csharp
public override async Task OnInitializing()
{
     .....
     Page.PulledToRefresh.Handle(Reload); // Attaches Reload() to the PulledToRefresh event of the page
}
```
<br>

### Properties
| Property     | Type         | Android | iOS | Windows |
| :----------- | :----------- | :------ | :-- | :------ |
| DataSource           | string          | x       | x   | x       |
| LazyLoadOffset | int | x | x | x |


### Events
| Event             | Type                                          | Android | iOS | Windows |
| :-----------      | :-----------                                  | :------ | :-- | :------ |
| on-Flashed            | AsyncEvent    | x       | x   | x       |
| on-Initializing            | AsyncEvent    | x       | x   | x       |
| on-LongPressed            | AsyncEvent    | x       | x   | x       |
| on-PanFinished            | AsyncEvent    | x       | x   | x       |
| on-Panning            | AsyncEvent   | x       | x   | x       |
| on-PreRendered            | AsyncEvent    | x       | x   | x       |
| on-Swiped            | AsyncEvent   | x       | x   | x       |
| on-Tapped            | AsyncEvent    | x       | x   | x       |


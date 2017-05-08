namespace Sitecore.Support.XA.Feature.PageStructure.Commands
{  
  using Sitecore;
  using Sitecore.Configuration;
  using Sitecore.Data;
  using Sitecore.Data.Items;
  using Sitecore.Layouts;
  using Sitecore.Shell.Applications.WebEdit.Commands;
  using Sitecore.Shell.Framework.Commands;
  using Sitecore.Text;
  using Sitecore.Web;
  using Sitecore.Web.UI.Sheer;
  using Sitecore.XA.Feature.PageStructure;
  using Sitecore.XA.Foundation.DynamicPlaceholders.Services;
  using Sitecore.XA.Foundation.Grid;
  using Sitecore.XA.Foundation.IoC;
  using Sitecore.XA.Foundation.SitecoreExtensions.Extensions;
  using Sitecore.XA.Foundation.SitecoreExtensions.Repositories;
  using System;
  using System.Collections.Specialized;
  using System.Linq;
  using System.Runtime.CompilerServices;
  using System.Runtime.InteropServices;
  using System.Xml;

  public abstract class SplitterResize : WebEditCommand
  {
    private readonly int _delta;
    private int _maxSize;
    private int _minSize;

    protected SplitterResize(int delta)
    {
      this._delta = delta;
    }

    public override void Execute(CommandContext context)
    {
      string id = ShortID.Decode(WebUtil.GetFormValue("scDeviceID"));
      LayoutDefinition definition = LayoutDefinition.Parse(WebEditUtil.ConvertJSONLayoutToXML(WebUtil.GetFormValue("scLayout")));
      if (definition == null)
      {
        this.ReturnLayout(null);
      }
      else
      {
        DeviceDefinition device = definition.GetDevice(id);
        if (device == null)
        {
          this.ReturnLayout(null);
        }
        else
        {
          RenderingDefinition renderingByUniqueId = device.GetRenderingByUniqueId(context.Parameters["referenceId"]);
          if (renderingByUniqueId == null)
          {
            this.ReturnLayout(null);
          }
          else
          {
            bool isColumnSplitter = true;
            if (string.IsNullOrEmpty(renderingByUniqueId.Parameters))
            {
              if (!string.IsNullOrEmpty(renderingByUniqueId.ItemID))
              {
                RenderingItem item = Sitecore.Client.ContentDatabase.GetItem(renderingByUniqueId.ItemID);
                renderingByUniqueId.Parameters = (item != null) ? item.Parameters : string.Empty;
              }
              else
              {
                renderingByUniqueId.Parameters = string.Empty;
              }
            }
            isColumnSplitter = IsColumnSplitter(renderingByUniqueId.ItemID);
            NameValueCollection parameters = WebUtil.ParseUrlParameters(renderingByUniqueId.Parameters);
            int size = MainUtil.GetInt(parameters["SplitterSize"], this.MinSize) + this._delta;
            if ((size < this.MinSize) || (size > this.MaxSize))
            {
              this.ReturnLayout(null);
            }
            else
            {
              parameters["SplitterSize"] = size.ToString();
              if (this._delta == 1)
              {
                this.FillGridDefaultValues(context, parameters, size, isColumnSplitter);
              }
              renderingByUniqueId.Parameters = new UrlString(parameters).GetUrl();
              string layout = WebEditUtil.ConvertXMLLayoutToJSON(definition.ToXml());
              this.ReturnLayout(layout);
            }
          }
        }
      }
    }

    private bool IsColumnSplitter(string itemId)
    {
      RenderingItem rendering = Sitecore.Client.ContentDatabase.GetItem(itemId);
      if (rendering != null)
      {
        return rendering.Name.Equals("Column Splitter", StringComparison.InvariantCultureIgnoreCase);
      }

      return true;
    }

    protected virtual void FillGridDefaultValues(CommandContext context, NameValueCollection parameters, int size, bool isColumnSplitter = false)
    {
      IContentRepository repository = ServiceLocator.Current.Resolve<IContentRepository>();
      IColumnWidthParameterService service = ServiceLocator.Current.Resolve<IColumnWidthParameterService>();
      Sitecore.Data.Items.Item gridDefinitionItem = ServiceLocator.Current.Resolve<IGridContext>().GetGridDefinitionItem(context.Items.First<Sitecore.Data.Items.Item>(), repository.GetItem(ShortID.Decode(WebUtil.GetFormValue("scDeviceID"))));
      if (gridDefinitionItem != null)
      {
        Sitecore.Data.Items.Item item = gridDefinitionItem.Database.GetItem(Sitecore.XA.Feature.PageStructure.Items.DefaultColumnLayoutDefinitionsRoot);
        if (item != null && isColumnSplitter)
        {
          char[] trimChars = new char[] { '|' };
          string str = (from item1 in item.ChildrenInheritingFrom(Sitecore.XA.Feature.PageStructure.Templates.Defaultcolumnlayout.ID)
                        //where (item1[Sitecore.XA.Feature.PageStructure.Templates.Defaultcolumnlayout.Fields.GridDefinition] != null)
                        where !string.IsNullOrEmpty(item1[Sitecore.XA.Feature.PageStructure.Templates.Defaultcolumnlayout.Fields.GridDefinition])                         
                        select item1).FirstOrDefault<Sitecore.Data.Items.Item>(item1 => new ID(item1[Sitecore.XA.Feature.PageStructure.Templates.Defaultcolumnlayout.Fields.GridDefinition]).Equals(gridDefinitionItem.ID))[Sitecore.XA.Foundation.Grid.Templates.GridDefinition.Fields.DefaultGridParameters].TrimEnd(trimChars);
          string columnWidthParameter = service.GetColumnWidthParameter(size - 1);
          if (parameters.AllKeys.Contains<string>(columnWidthParameter))
          {
            parameters[columnWidthParameter] = str;
          }
          else
          {
            parameters.Add(columnWidthParameter, str);
          }
        }
      }
    }

    protected virtual void ReadMinMaxSize()
    {
      XmlNode configNode = Factory.GetConfigNode("experienceAccelerator/splitterResizer");
      if ((configNode == null) || (configNode.Attributes == null))
      {
        this._minSize = 1;
        this._maxSize = 0x7fffffff;
      }
      else
      {
        XmlAttribute attribute = configNode.Attributes["minSize"];
        if (attribute != null)
        {
          this._minSize = MainUtil.GetInt(attribute.Value ?? string.Empty, 1);
        }
        else
        {
          this._minSize = 1;
        }
        XmlAttribute attribute2 = configNode.Attributes["maxSize"];
        if (attribute2 != null)
        {
          this._maxSize = MainUtil.GetInt(attribute2.Value ?? string.Empty, 0x7fffffff);
        }
        else
        {
          this._maxSize = 0x7fffffff;
        }
      }
    }

    protected virtual void ReturnLayout(string layout = null)
    {
      SheerResponse.SetAttribute("scLayoutDefinition", "value", layout ?? string.Empty);
      if (!string.IsNullOrEmpty(layout))
      {
        SheerResponse.Eval("window.parent.Sitecore.PageModes.ChromeManager.handleMessage('chrome:rendering:propertiescompleted');");
      }
    }

    protected virtual int MaxSize
    {
      get
      {
        if (this._maxSize == 0)
        {
          this.ReadMinMaxSize();
        }
        return this._maxSize;
      }
    }

    protected virtual int MinSize
    {
      get
      {
        if (this._minSize == 0)
        {
          this.ReadMinMaxSize();
        }
        return this._minSize;
      }
    }
  }
}

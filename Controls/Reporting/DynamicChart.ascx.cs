// <copyright>
// Copyright by the Spark Development Network
//
// Licensed under the Rock Community License (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.rockrms.com/license
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>
//
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;

using Rock;
using Rock.Attribute;
using Rock.Data;
using Rock.Model;
using Rock.Web.Cache;
using Rock.Web.UI.Controls;

namespace RockWeb.Plugins.com_shepherdchurch.Reporting
{
    [DisplayName( "Dynamic Chart" )]
    [Category( "com_shepherdchurch > Custom Core > Reporting" )]
    [Description( "Block to display a chart using SQL as the chart datasource" )]

    [CodeEditorField( "SQL", @"The SQL for the datasource. Output columns must be as follows:
<ul>
    <li>Bar or Line Chart
        <ul>
           <li>[SeriesName] : string or numeric </li>
           <li>[DateTime] : DateTime </li>
           <li>[YValue] : numeric </li>
        </ul>
    </li>
    <li>Pie Chart
        <ul>
           <li>[MetricTitle] : string </li>
           <li>[YValueTotal] : numeric </li>
        </ul>
    </li>
</ul>

Example: 
<code><pre>
-- get top 25 viewed pages from the last 30 days (excluding Home)
select top 25  * from (
    select 
        distinct
        pv.PageTitle [SeriesName], 
        convert(date, pv.DateTimeViewed) [DateTime], 
        count(*) [YValue] 
    from 
        PageView pv
    where PageTitle is not null    
    group by pv.PageTitle, convert(date, pv.DateTimeViewed)
    ) x where SeriesID != 'Home' 
and DateTime > DateAdd(day, -30, SysDateTime())
order by YValue desc
</pre>
</code>",
              CodeEditorMode.Sql )]

    [IntegerField( "Chart Height", "", false, 200 )]
    [DefinedValueField( Rock.SystemGuid.DefinedType.CHART_STYLES, "Chart Style", order: 3 )]

    [BooleanField( "Show Legend", "", true, order: 7 )]
    [CustomDropdownListField( "Legend Position", "Select the position of the Legend (corner)", "ne,nw,se,sw", false, "ne", order: 8 )]
    [CustomDropdownListField( "Chart Type", "", "Line,Bar,Pie", false, "Line", order: 9 )]
    [DecimalField( "Pie Inner Radius", "If this is a pie chart, specific the inner radius to have a donut hole. For example, specify: 0.75 to have the inner radius as 75% of the outer radius.", false, 0, order: 10 )]
    [BooleanField( "Pie Show Labels", "If this is a pie chart, specify if labels show be shown", true, "", order: 11 )]
    public partial class DynamicChart : Rock.Reporting.Dashboard.DashboardWidget
    {
        /// <summary>
        /// Raises the <see cref="E:System.Web.UI.Control.Init" /> event.
        /// </summary>
        /// <param name="e">An <see cref="T:System.EventArgs" /> object that contains the event data.</param>
        protected override void OnInit( EventArgs e )
        {
            string tooltipFormatter = @"
function(item) {
    if (item.series.chartData) {
        if (item.series.chartData[item.dataIndex].DateTimeStamp) {
            tooltipText = new Date(item.series.chartData[item.dataIndex].DateTimeStamp).toLocaleDateString();
        };

        if (item.series.chartData[item.dataIndex].StartDateTimeStamp) {
            tooltipText = new Date(item.series.chartData[item.dataIndex].StartDateTimeStamp).toLocaleDateString();
        }

        if (item.series.chartData[item.dataIndex].EndDateTimeStamp) {
            tooltipText += ' to ' + new Date(item.series.chartData[item.dataIndex].EndDateTimeStamp).toLocaleDateString();
        }

        if (item.series.chartData[item.dataIndex].MetricTitle) {
            tooltipText = item.series.chartData[item.dataIndex].MetricTitle;
        }
    }

    if (tooltipText) {
        tooltipText += '<br />';
    }

    if (item.series.label) {
        tooltipText += item.series.label;
    }

    if (item.series.chartData) {
        var pointValue = item.series.chartData[item.dataIndex].YValueFormatted || item.series.chartData[item.dataIndex].YValue || item.series.chartData[item.dataIndex].YValueTotal || '';
        
        tooltipText += ': ' + pointValue;

        if (item.series.chartData[item.dataIndex].Note) {
            tooltipText += '<br />' + item.series.chartData[item.dataIndex].Note;
        }
    }

    return tooltipText;
}
";

            base.OnInit( e );

            // this event gets fired after block settings are updated. it's nice to repaint the screen if these settings would alter it
            this.BlockUpdated += Block_BlockUpdated;
            this.AddConfigurationUpdateTrigger( upnlContent );

            var pageReference = new Rock.Web.PageReference( this.PageCache.Id );
            pageReference.QueryString = new System.Collections.Specialized.NameValueCollection();
            pageReference.QueryString.Add( "GetChartData", "true" );
            pageReference.QueryString.Add( "GetChartDataBlockId", this.BlockId.ToString() );
            pageReference.QueryString.Add( "TimeStamp", RockDateTime.Now.ToJavascriptMilliseconds().ToString() );
            lcLineChart.DataSourceUrl = pageReference.BuildUrl();
            lcLineChart.ChartHeight = this.GetAttributeValue( "ChartHeight" ).AsIntegerOrNull() ?? 200;
            lcLineChart.Options.SetChartStyle( this.GetAttributeValue( "ChartStyle" ).AsGuidOrNull() );
            lcLineChart.Options.legend = lcLineChart.Options.legend ?? new Legend();
            lcLineChart.Options.legend.show = this.GetAttributeValue( "ShowLegend" ).AsBooleanOrNull();
            lcLineChart.Options.legend.position = this.GetAttributeValue( "LegendPosition" );
            lcLineChart.TooltipFormatter = tooltipFormatter;

            bcBarChart.DataSourceUrl = pageReference.BuildUrl();
            bcBarChart.ChartHeight = this.GetAttributeValue( "ChartHeight" ).AsIntegerOrNull() ?? 200;
            bcBarChart.Options.SetChartStyle( this.GetAttributeValue( "ChartStyle" ).AsGuidOrNull() );
            bcBarChart.Options.xaxis = new AxisOptions { mode = AxisMode.categories, tickLength = 0 };
            bcBarChart.Options.series.bars.barWidth = 0.6;
            bcBarChart.Options.series.bars.align = "center";
            bcBarChart.TooltipFormatter = tooltipFormatter;

            bcBarChart.Options.legend = lcLineChart.Options.legend ?? new Legend();
            bcBarChart.Options.legend.show = this.GetAttributeValue( "ShowLegend" ).AsBooleanOrNull();
            bcBarChart.Options.legend.position = this.GetAttributeValue( "LegendPosition" );

            pcPieChart.DataSourceUrl = pageReference.BuildUrl();
            pcPieChart.ChartHeight = this.GetAttributeValue( "ChartHeight" ).AsIntegerOrNull() ?? 200;
            pcPieChart.Options.SetChartStyle( this.GetAttributeValue( "ChartStyle" ).AsGuidOrNull() );
            pcPieChart.TooltipFormatter = tooltipFormatter;

            pcPieChart.PieOptions.label = new PieLabel { show = this.GetAttributeValue( "PieShowLabels" ).AsBooleanOrNull() ?? true };
            pcPieChart.PieOptions.label.formatter = @"
function labelFormatter(label, series) {
	return ""<div style='font-size:8pt; text-align:center; padding:2px; '>"" + label + ""<br/>"" + Math.round(series.percent) + ""%</div>"";
}
".Trim();
            pcPieChart.Legend.show = this.GetAttributeValue( "ShowLegend" ).AsBooleanOrNull();

            pcPieChart.PieOptions.innerRadius = this.GetAttributeValue( "PieInnerRadius" ).AsDoubleOrNull();

            lcLineChart.Visible = false;
            bcBarChart.Visible = false;
            pcPieChart.Visible = false;
            var chartType = this.GetAttributeValue( "ChartType" );
            if ( chartType == "Pie" )
            {
                pcPieChart.Visible = true;
            }
            else if ( chartType == "Bar" )
            {
                bcBarChart.Visible = true;
            }
            else
            {
                lcLineChart.Visible = true;
            }

            pnlDashboardTitle.Visible = !string.IsNullOrEmpty( this.Title );
            pnlDashboardSubtitle.Visible = !string.IsNullOrEmpty( this.Subtitle );
            lDashboardTitle.Text = this.Title;
            lDashboardSubtitle.Text = this.Subtitle;

            var sql = this.GetAttributeValue( "SQL" );

            if ( string.IsNullOrWhiteSpace( sql ) )
            {
                nbConfigurationWarning.Visible = true;
                nbConfigurationWarning.Text = "SQL needs to be configured in block settings";
            }
            else
            {
                nbConfigurationWarning.Visible = false;
            }

            if ( PageParameter( "GetChartData" ).AsBoolean() && ( PageParameter( "GetChartDataBlockId" ).AsInteger() == this.BlockId ) )
            {
                GetChartData();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public class DynamicChartData : Rock.Chart.IChartData
        {
            /// <summary>
            /// Gets the date time stamp.
            /// </summary>
            /// <value>
            /// The date time stamp.
            /// </value>
            public long DateTimeStamp { get; set; }

            /// <summary>
            /// Gets the y value (for Line and Bar Charts)
            /// </summary>
            /// <value>
            /// The y value.
            /// </value>
            public decimal? YValue { get; set; }

            /// <summary>
            /// Gets the y value as a formatted string (for Line and Bar Charts)
            /// </summary>
            /// <value>
            /// The formatted y value.
            /// </value>
            public string YValueFormatted { get; set; }

            /// <summary>
            /// Gets or sets the metric title (for pie charts)
            /// </summary>
            /// <value>
            /// The metric title.
            /// </value>
            public string MetricTitle { get; set; }

            /// <summary>
            /// Gets the y value (for pie charts)
            /// </summary>
            /// <value>
            /// The y value.
            /// </value>
            public decimal? YValueTotal { get; set; }

            /// <summary>
            /// Gets the series identifier (obsolete)
            /// NOTE: Use MetricValuePartitionEntityIds if you are populating this with a EntityTypeId|EntityId list, or use SeriesName for a static series name
            /// </summary>
            /// <value>
            /// The series identifier.
            /// </value>
            [Obsolete( "Use MetricValuePartitionEntityIds if you are populating this with a EntityTypeId|EntityId list, or use SeriesName for a static series name" )]
            public string SeriesId { get; set; }

            /// <summary>
            /// Gets or sets the name of the series. This will be the default name of the series if MetricValuePartitionEntityIds can't be resolved
            /// </summary>
            /// <value>
            /// The name of the series.
            /// </value>
            public string SeriesName { get; set; }

            /// <summary>
            /// Gets the metric value partitions as a comma-delimited list of EntityTypeId|EntityId
            /// </summary>
            /// <value>
            /// The metric value entityTypeId,EntityId partitions
            /// </value>
            public string MetricValuePartitionEntityIds { get; set; }
        }

        /// <summary>
        /// Gets the chart data (ajax call from Chart)
        /// </summary>
        private void GetChartData()
        {
            try
            {
                var sql = this.GetAttributeValue( "SQL" );

                if ( string.IsNullOrWhiteSpace( sql ) )
                {
                    //
                }
                else
                {
                    var mergeFields = GetDynamicDataMergeFields();
                    sql = sql.ResolveMergeFields( mergeFields );

                    DataSet dataSet = DbService.GetDataSet( sql, System.Data.CommandType.Text, null );
                    List<DynamicChartData> chartDataList = new List<DynamicChartData>();
                    foreach ( var row in dataSet.Tables[0].Rows.OfType<DataRow>() )
                    {
                        var chartData = new DynamicChartData();

                        if ( row.Table.Columns.Contains( "SeriesName" ) )
                        {
                            chartData.SeriesName = Convert.ToString( row["SeriesName"] );
                        }
                        else if ( row.Table.Columns.Contains( "SeriesID" ) )
                        {
                            // backwards compatibility
                            chartData.SeriesName = Convert.ToString( row["SeriesID"] );
                        }

                        if ( row.Table.Columns.Contains( "YValue" ) )
                        {
                            chartData.YValue = Convert.ToDecimal( row["YValue"] );
                        }

                        if ( row.Table.Columns.Contains( "MetricTitle" ) )
                        {
                            chartData.MetricTitle = Convert.ToString( row["MetricTitle"] );
                        }
                        else
                        {
                            chartData.MetricTitle = chartData.SeriesName;
                        }

                        if ( row.Table.Columns.Contains( "YValueTotal" ) )
                        {
                            chartData.YValueTotal = Convert.ToDecimal( row["YValueTotal"] );
                        }
                        else
                        {
                            chartData.YValueTotal = chartData.YValue;
                        }

                        if ( row.Table.Columns.Contains( "YValueFormatted" ) )
                        {
                            chartData.YValueFormatted = Convert.ToString( row["YValueFormatted"] );
                        }
                        else
                        {
                            chartData.YValueFormatted = chartData.YValue.HasValue ? chartData.YValue.Value.ToString( "G29" ) : string.Empty;
                        }

                        if ( row.Table.Columns.Contains( "DateTime" ) )
                        {
                            chartData.DateTimeStamp = ( row["DateTime"] as DateTime? ).Value.ToJavascriptMilliseconds();
                        }
                        else if ( row.Table.Columns.Contains( "XValue" ) )
                        {
                            chartData.DateTimeStamp = ( row["XValue"] as int? ).Value;
                        }

                        chartDataList.Add( chartData );
                    }

                    chartDataList = chartDataList.OrderBy( a => a.SeriesName ).ThenBy( a => a.DateTimeStamp ).ToList();

                    Response.Clear();
                    Response.Write( chartDataList.ToJson() );
                    Response.End();
                }
            }
            catch ( System.Threading.ThreadAbortException )
            {
                // ignore the ThreadAbort exception from Response.End();
            }
            catch ( Exception ex )
            {
                LogException( ex );
                throw;
            }
        }

        /// <summary>
        /// Handles the BlockUpdated event of the Block control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void Block_BlockUpdated( object sender, EventArgs e )
        {
            // reload the full page since controls are dynamically created based on block settings
            NavigateToPage( this.CurrentPageReference );
        }

        /// <summary>
        /// Gets the dynamic data merge fields.
        /// </summary>
        /// <returns></returns>
        private Dictionary<string, object> GetDynamicDataMergeFields()
        {
            var mergeFields = Rock.Lava.LavaHelper.GetCommonMergeFields( this.RockPage, this.CurrentPerson );

            mergeFields.Add( "RockVersion", Rock.VersionInfo.VersionInfo.GetRockProductVersionNumber() );
            mergeFields.AddOrReplace( "CurrentPage", this.PageCache );

            return mergeFields;
        }
    }
}

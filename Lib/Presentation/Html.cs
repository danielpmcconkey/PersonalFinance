using System.Text;
using Lib.DataTypes.Presentation;
using Lib.StaticConfig;

namespace Lib.Presentation;

public static class Html
{
    public static string CreateOverallHtml(string financialSummary, List<PresChart> charts)
    {
        string html = $"""
            <!doctype html>
            <html lang="en">

                <head>
                    <meta charset="utf-8">
                    <meta name="viewport" content="width=device-width, initial-scale=1">
                    <title>McConkey family finance summary</title>

                    <link href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/css/bootstrap.min.css" rel="stylesheet" integrity="sha384-T3c6CoIi6uLrA9TneNEoa7RxnatzjcDSCmG1MXxSR1GAsXEV/Dwwykc2MPK8M2HN" crossorigin="anonymous">
                    {CreateChartsHead(charts)}
                    {CreateCss()}
                </head>
                <body>
                    <h1 id="content" class="bd-title">McConkey family finance summary as of {
                        //_builder.GetEffectiveDate().ToDateTimeUnspecified():MMMM dd, yyyy
                        DateTime.Now
                    }</h1>
                    
                    <div class=bd-content">
                        <ul class="nav nav-tabs" id="myTab" role="tablist">
                            <li class="nav-item" role="presentation">
                                <button class="nav-link active" id="wealth-tab" data-bs-toggle="tab" data-bs-target="#wealth-tab-pane" type="button" role="tab" aria-controls="wealth-tab-pane" aria-selected="true">Net worth and investments</button>
                            </li>
                            <li class="nav-item" role="presentation">
                                <button class="nav-link" id="chart-tab" data-bs-toggle="tab" data-bs-target="#chart-tab-pane" type="button" role="tab" aria-controls="chart-tab-pane" aria-selected="false">Charts</button>
                            </li>
                        </ul>

                        <div class="tab-content" id="myTabContent">
                            <div class="tab-pane fade show active" id="wealth-tab-pane" role="tabpanel" aria-labelledby="wealth-tab" tabindex="0">
                                <div class="border rounded-3">
                                    <div class="chartSpace">
                                        {financialSummary}
                                    </div>
                                </div>
                            </div><!--end wealth-tab-pane -->

                            

                            <div class="tab-pane fade" id="chart-tab-pane" role="tabpanel" aria-labelledby="chart-tab" tabindex="2">
                                <div class="border rounded-3">
                                    <div class="chartSpace">
                                        {CreateChartBody(charts)}
                                    </div>
                                </div>                           
                            </div><!--end chart-tab-pane -->

                        </div><!--end myTabContent -->
                    </div>
                    <script src="https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/js/bootstrap.bundle.min.js" integrity="sha384-C6RzsynM9kWDrMNeT87bh95OGNyZPhcTNXj1NW7RuBCsyN/o0jlpcV8Qyq46cDfL" crossorigin="anonymous"></script>
                </body>
            </html>
            """;

        return html;

    }
    private static string CreateCss()
    {
        string output = """
            <style type="text/css">
                body { padding:25px; }
                h1 { margin:25px; }
                #myTab { margin-top:25px; }
                .textSpace {
                    border:solid 3px black;
                    background:#ffffff;
                    margin-bottom:40px;
                    padding:40px;
                }
                .chartSpace {
                    margin-bottom:40px;
                    padding:40px;
                }
                .chartDescription {
                    font-size:20px;
                    width:60%;
                    margin-left:15%;
                }
                
                .summaryTable tbody th { text-align:left; width:400px;}
                .summaryTable tbody td { text-align:right; width:400px; }
                .summaryTable tbody th.level0 { padding-left:0px; font-weight:bold; font-size:24px; }
                .summaryTable tbody td.level0 { padding-right:0px; font-weight:bold; font-size:24px; }
                .summaryTable tbody th.level1 { padding-left:25px; font-weight:bold; }
                .summaryTable tbody td.level1 { padding-right:0px; font-weight:bold; }
                .summaryTable tbody th.level2 { padding-left:50px; font-weight:normal; }
                .summaryTable tbody td.level2 { padding-right:0px; font-weight:normal; }
                .summaryTable tbody th.suml0 { border-top:solid 3px black; }
                .summaryTable tbody td.suml0 { border-top:solid 3px black; }
                .summaryTable tbody th.suml1 { border-top:solid 1px black; }
                .summaryTable tbody td.suml1 { border-top:solid 1px black; }
                .summaryTable tbody tr.ledgerline { border-bottom:solid 1px #cccccc; }
                .summaryTable tbody th.monthHead { text-align:right; }
                .tab-content>.tab-pane {
                    height: 1px;
                    overflow: hidden;
                    display: block;
                    visibility: hidden;
                }
                .tab-content>.active {
                    height: auto;
                    overflow: auto;
                    visibility: visible;
                }

            </style>
            """;
        
        return output;
    }
    private static string CreateChartsHead(List<PresChart> charts)
    {
        var output = new StringBuilder();
        output.AppendLine("<script type=\"text/javascript\" src=\"https://www.gstatic.com/charts/loader.js\"></script>");
        output.AppendLine("<script type=\"text/javascript\">");
        output.AppendLine("google.charts.load('current', { 'packages':['corechart']});");
        output.AppendLine("google.charts.load('current', {'packages':['bar']});");
        // draw the charts when the page loads
        foreach (var c in charts.OrderBy(x => x.Ordinal))
        {
            output.AppendLine($"google.charts.setOnLoadCallback(drawChart{c.Ordinal});");
        }
            
        // populate the chart data
        foreach (var c in charts.OrderBy(x => x.Ordinal))
        {
            output.AppendLine(CreateChartHead(c));
        }
        
        output.AppendLine("    </script>");
        return output.ToString();
    }

    private static string CreateChartDataJs(PresChart c)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("var data = google.visualization.arrayToDataTable([");
        sb.Append("[");
        for (var i = 0; i < c.ChartDataGrid.ColumnNames.Length; i++)
        {
            var delimiter = (i > 0) ? "," : string.Empty;
            sb.Append($"{delimiter} '{c.ChartDataGrid.ColumnNames[i]}' ");
        }
        sb.AppendLine("],");

        for (var i = 0; i < c.ChartDataGrid.Data.Length; i++)
        {
            var row = c.ChartDataGrid.Data[i];
            sb.Append($"['{row[0]}'");
            for (var j = 1; j < row.Length; j++)
            {
                sb.Append($", {row[j]}");
            }
            var delimiter = (i == c.ChartDataGrid.Data.Length - 1) ? string.Empty : ",";
            sb.AppendLine($"]{delimiter}");
        }
        sb.AppendLine("]);");
        
        return sb.ToString();
    }
    private static string CreateChartHead(PresChart c)
    {
        StringBuilder output = new StringBuilder();
        output.AppendLine($"      function drawChart{c.Ordinal}() {"{"}");
        output.AppendLine(CreateChartDataJs(c));
        output.AppendLine("");
        var chartOptions = string.Empty;
        switch (c.PresChartType)
        {
            case PresChartType.Area:
                chartOptions = $$$"""


                                          var options = {title:'{{{c.Title}}}',
                                                         hAxis:{title: 'Month',  titleTextStyle: {color: '#333'}},
                                                         vAxis: {title: '{{{c.VAxisTitle}}}', minValue: 0}, 
                                                         isStacked:true,
                                                         width:1500,
                                                         height:500,
                                                         explorer: { actions: ['dragToZoom', 'rightClickToReset']	},
                                                         focusTarget: 'category'
                                                         };

                                          var chart = new google.visualization.AreaChart(document.getElementById('chart_{{{c.Ordinal}}}_div'));
                                          chart.draw(data, options);

                                  """;
                break;
            case PresChartType.Bar:
                chartOptions = $$$"""
                                  
                                  
                                          var options = { width:1500, height:500, chart: { title:'{{{c.Title}}}' } };
                                          var chart = new google.charts.Bar(document.getElementById('chart_{{{c.Ordinal}}}_div'));
                                          chart.draw(data, google.charts.Bar.convertOptions(options));

                                  """;
                break;
            case PresChartType.Line:
                chartOptions = $$$"""


                                          var options = {title:'{{{c.Title}}}',
                                                         hAxis:{title: 'Month',  titleTextStyle: {color: '#333'}},
                                                         vAxis: {title: '{{{c.VAxisTitle}}}', minValue: 0}, 
                                                         isStacked:false,
                                                         width:1500,
                                                         height:500,
                                                         explorer: { actions: ['dragToZoom', 'rightClickToReset']	},
                                                         focusTarget: 'category'
                                                         };

                                          var chart = new google.visualization.LineChart(document.getElementById('chart_{{{c.Ordinal}}}_div'));
                                          chart.draw(data, options);

                                  """;
                break;
            case PresChartType.Pie:
                chartOptions = $$$"""


                                          var options = { width:1500, height:500, title:'{{{c.Title}}}' };
                                          var chart = new google.visualization.PieChart(document.getElementById('chart_{{{c.Ordinal}}}_div'));
                                          chart.draw(data, options);

                                  """;
                break;
            default:
                throw new NotImplementedException("other chart types not implemented yet");
        }
        output.AppendLine(chartOptions);
        output.AppendLine("      }");


        return output.ToString();
    }
    public static string CreateChartBody(List<PresChart> charts)
    {
        StringBuilder output = new StringBuilder();
        
        output.AppendLine($"    <p class=\"chartSpace\">Model champion: {MonteCarloConfig.ChampionModelId}</p>");

        foreach (var c in charts.OrderBy(x => x.Ordinal))
        {
            output.AppendLine("    <div class=\"chartSpace\">");
            output.AppendLine($"    <div id=\"chart_{c.Ordinal}_div\" class=\"gchart\" ></div>");
            output.AppendLine($"    <p class=\"chartDescription\">{c.Description}</p>");
            output.AppendLine("    </div>");
        }
        return output.ToString();
    }
}
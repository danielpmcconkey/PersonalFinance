namespace Lib.Presentation;

public static class Html
{
    public static string CreateOverallHtml(string financialSummary, string budgetSummary)
    {
        string html = $"""
            <!doctype html>
            <html lang="en">

                <head>
                    <meta charset="utf-8">
                    <meta name="viewport" content="width=device-width, initial-scale=1">
                    <title>McConkey family finance summary</title>

                    <link href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/css/bootstrap.min.css" rel="stylesheet" integrity="sha384-T3c6CoIi6uLrA9TneNEoa7RxnatzjcDSCmG1MXxSR1GAsXEV/Dwwykc2MPK8M2HN" crossorigin="anonymous">
                    {
                        //_builder.GetChartsHead()
                        ""
                    }
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
                                <button class="nav-link" id="spend-tab" data-bs-toggle="tab" data-bs-target="#spend-tab-pane" type="button" role="tab" aria-controls="spend-tab-pane" aria-selected="false">Budgets and spending</button>
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

                            <div class="tab-pane fade" id="spend-tab-pane" role="tabpanel" aria-labelledby="spend-tab" tabindex="1">
                                <div class="border rounded-3">
                                    <div class="chartSpace">
                                        {budgetSummary}
                                    </div>
                                </div>
                            </div><!--end spend-tab-pane -->

                            <div class="tab-pane fade" id="chart-tab-pane" role="tabpanel" aria-labelledby="chart-tab" tabindex="2">
                                <div class="border rounded-3">
                                    <div class="chartSpace">
                                        {
                                            //_builder.GetCharts()
                                            ""
                                        }
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
}
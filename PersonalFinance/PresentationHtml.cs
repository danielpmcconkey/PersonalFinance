using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PersonalFinance
{
    internal class PresentationHtml
    {
        private PresentationBuilder _builder;
        internal PresentationHtml(PresentationBuilder builder)
        {
            _builder = builder;
        }
        internal string GetHTML()
        {
            string html = $"""
                <!doctype html>
                <html lang="en">

                    <head>
                        <meta charset="utf-8">
                        <meta name="viewport" content="width=device-width, initial-scale=1">
                        <title>McConkey family finance summary</title>

                        <link href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/css/bootstrap.min.css" rel="stylesheet" integrity="sha384-T3c6CoIi6uLrA9TneNEoa7RxnatzjcDSCmG1MXxSR1GAsXEV/Dwwykc2MPK8M2HN" crossorigin="anonymous">
                        {_builder.GetChartsHead()}
                        {_builder.GetCss()}
                    </head>
                    <body>
                        <h1 id="content" class="bd-title">McConkey family finance summary as of {_builder.GetEffectiveDate().ToString("MMMM dd, yyyy")}</h1>
                        
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
                                            {_builder.GetFinancialSummary()}
                                        </div>
                                    </div>
                                </div><!--end wealth-tab-pane -->

                                <div class="tab-pane fade" id="spend-tab-pane" role="tabpanel" aria-labelledby="spend-tab" tabindex="1">
                                    <div class="border rounded-3">
                                        <div class="chartSpace">
                                            {_builder.GetBudgetSummary()}
                                        </div>
                                    </div>
                                </div><!--end spend-tab-pane -->

                                <div class="tab-pane fade" id="chart-tab-pane" role="tabpanel" aria-labelledby="chart-tab" tabindex="2">
                                    <div class="border rounded-3">
                                        <div class="chartSpace">
                                            {_builder.GetCharts()}
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
    }
}

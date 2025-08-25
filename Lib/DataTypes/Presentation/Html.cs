namespace Lib.DataTypes.Presentation;

public static class Html
{
    public static string GetCss()
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
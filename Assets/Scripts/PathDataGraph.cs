using UnityEngine;
using XCharts.Runtime;

public class PathDataGraph : MonoBehaviour
{
    public LineChart chart; 
    private int step = 0;


    void Start()
    {   
        chart.RemoveData();
        chart.AddSerie<Line>("CalcTime");
        chart.AddSerie<Line>("PathTime");
        chart.AddSerie<Line>("Distanza");
    }

    public void AddDataPoint(float tempoCalcolo, float tempoPercorso, float distanza)
    {
        chart.AddData(0, step, tempoCalcolo);
        chart.AddData(1, step, tempoPercorso);
        chart.AddData(2, step, distanza);
        step++;
    }
}

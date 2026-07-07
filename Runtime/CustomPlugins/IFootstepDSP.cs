using System.Collections.Generic;

public class DSPParameter
{
    public string name;
    public float value;
    public float minValue;
    public float maxValue;
    public string tooltip;

    public DSPParameter(string name, float defaultValue, float minValue, float maxValue, string tooltip = "")
    {
        this.name = name;
        this.value = defaultValue;
        this.minValue = minValue;
        this.maxValue = maxValue;
        this.tooltip = tooltip;
    }
}

public interface IFootstepDSP
{
    string Name { get; }
    bool Enabled { get; set; }
    List<DSPParameter> Parameters { get; }
    float[] Apply(float[] samples, int channels, int frequency);
}

using UnityEngine;

public static class Curves
{
    public static float Linear(float t) => t;

    #region Helpers

    private static float InOut(System.Func<float, float> easeIn, float t)
    {
        if (t < 0.5f)
            return easeIn(t * 2f) * 0.5f;

        return 1f - easeIn((1f - t) * 2f) * 0.5f;
    }

    #endregion

    #region Power

    public static float InPower(float t, float power)
        => Mathf.Pow(t, power);

    public static float OutPower(float t, float power)
        => 1f - InPower(1f - t, power);

    public static float InOutPower(float t, float power)
        => InOut(x => InPower(x, power), t);

    public static float InQuad(float t) => InPower(t, 2f);
    public static float OutQuad(float t) => OutPower(t, 2f);
    public static float InOutQuad(float t) => InOutPower(t, 2f);

    public static float InCubic(float t) => InPower(t, 3f);
    public static float OutCubic(float t) => OutPower(t, 3f);
    public static float InOutCubic(float t) => InOutPower(t, 3f);

    public static float InQuart(float t) => InPower(t, 4f);
    public static float OutQuart(float t) => OutPower(t, 4f);
    public static float InOutQuart(float t) => InOutPower(t, 4f);

    public static float InQuint(float t) => InPower(t, 5f);
    public static float OutQuint(float t) => OutPower(t, 5f);
    public static float InOutQuint(float t) => InOutPower(t, 5f);

    #endregion

    #region Sine

    public static float InSine(float t)
        => 1f - Mathf.Cos(t * Mathf.PI * 0.5f);

    public static float OutSine(float t)
        => Mathf.Sin(t * Mathf.PI * 0.5f);

    public static float InOutSine(float t)
        => -(Mathf.Cos(Mathf.PI * t) - 1f) * 0.5f;

    #endregion

    #region Exponential

    public static float InExpo(float t)
    {
        if (t == 0f)
            return 0f;

        return Mathf.Pow(2f, 10f * (t - 1f));
    }

    public static float OutExpo(float t)
    {
        if (t == 1f)
            return 1f;

        return 1f - InExpo(1f - t);
    }

    public static float InOutExpo(float t)
        => InOut(InExpo, t);

    #endregion

    #region Circular

    public static float InCirc(float t)
        => 1f - Mathf.Sqrt(1f - t * t);

    public static float OutCirc(float t)
        => 1f - InCirc(1f - t);

    public static float InOutCirc(float t)
        => InOut(InCirc, t);

    #endregion

    #region Elastic

    public static float InElastic(float t)
        => 1f - OutElastic(1f - t);

    public static float OutElastic(float t)
    {
        if (t == 0f)
            return 0f;

        if (t == 1f)
            return 1f;

        const float p = 0.3f;

        return Mathf.Pow(2f, -10f * t)
             * Mathf.Sin((t - p * 0.25f) * (2f * Mathf.PI) / p)
             + 1f;
    }

    public static float InOutElastic(float t)
        => InOut(InElastic, t);

    #endregion

    #region Back

    public static float InBack(float t)
    {
        const float s = 1.70158f;
        return t * t * ((s + 1f) * t - s);
    }

    public static float OutBack(float t)
        => 1f - InBack(1f - t);

    public static float InOutBack(float t)
        => InOut(InBack, t);

    #endregion

    #region Bounce

    public static float InBounce(float t)
        => 1f - OutBounce(1f - t);

    public static float OutBounce(float t)
    {
        const float div = 2.75f;
        const float mult = 7.5625f;

        if (t < 1f / div)
        {
            return mult * t * t;
        }

        if (t < 2f / div)
        {
            t -= 1.5f / div;
            return mult * t * t + 0.75f;
        }

        if (t < 2.5f / div)
        {
            t -= 2.25f / div;
            return mult * t * t + 0.9375f;
        }

        t -= 2.625f / div;
        return mult * t * t + 0.984375f;
    }

    public static float InOutBounce(float t)
        => InOut(InBounce, t);

    #endregion

    #region Smooth

    public static float SmoothStep(float t)
        => t * t * (3f - 2f * t);

    public static float SmootherStep(float t)
        => t * t * t * (t * (6f * t - 15f) + 10f);

    #endregion
}
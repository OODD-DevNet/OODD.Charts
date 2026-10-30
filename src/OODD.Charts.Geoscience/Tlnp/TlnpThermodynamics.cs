using System;

namespace OODD.Charts.Geoscience.Tlnp
{
    /// <summary>
    /// T-lnP 的五族线与层结曲线所需的纯热力学函数。
    /// <para>
    /// 全部是解析式或定步长 RK4 积分，<b>不查表</b>（docs/06 D21：不内置未经核实的标准数值表）。
    /// 物理常数由 <see cref="SoundingConstants"/> 给出；改变常数只会改变线族形状，
    /// 不会改变“等压线等距 / 等温线斜直”的几何性质。
    /// </para>
    /// </summary>
    public static class TlnpThermodynamics
    {
        /// <summary>饱和水汽压（hPa），Magnus 式 e<sub>s</sub> = e₀·exp(aT/(b+T))。</summary>
        public static double SaturationVaporPressureHpa(double temperatureC, SoundingConstants constants)
        {
            if (constants == null)
            {
                throw new ArgumentNullException(nameof(constants));
            }

            return constants.SaturationPressureAtZeroC
                 * Math.Exp(constants.MagnusA * temperatureC / (temperatureC + constants.MagnusB));
        }

        /// <summary>饱和比湿（kg/kg）：εe<sub>s</sub>/(p−e<sub>s</sub>)。</summary>
        public static double SaturationMixingRatioKgKg(double temperatureC, double pressureHpa,
                                                       SoundingConstants constants)
        {
            if (!(pressureHpa > 0d))
            {
                throw new ArgumentOutOfRangeException(nameof(pressureHpa), "气压必须为正，实到 " + pressureHpa);
            }

            double es = SaturationVaporPressureHpa(temperatureC, constants);
            double denominator = pressureHpa - es;
            if (denominator <= 0d)
            {
                return double.PositiveInfinity;
            }
            return constants.Epsilon * es / denominator;
        }

        /// <summary>饱和比湿（g/kg）。</summary>
        public static double SaturationMixingRatioGPerKg(double temperatureC, double pressureHpa,
                                                         SoundingConstants constants)
        {
            return SaturationMixingRatioKgKg(temperatureC, pressureHpa, constants) * 1000d;
        }

        /// <summary>
        /// 由等饱和比湿反解温度（℃）：e = w·p/(ε+w)，再用 Magnus 式反解。
        /// 这是“等饱和比湿线”的参数方程来源。
        /// </summary>
        public static double TemperatureOfMixingRatio(double mixingRatioGPerKg, double pressureHpa,
                                                      SoundingConstants constants)
        {
            if (constants == null)
            {
                throw new ArgumentNullException(nameof(constants));
            }
            if (!(mixingRatioGPerKg > 0d))
            {
                throw new ArgumentOutOfRangeException(nameof(mixingRatioGPerKg),
                    "比湿必须为正，实到 " + mixingRatioGPerKg);
            }
            if (!(pressureHpa > 0d))
            {
                throw new ArgumentOutOfRangeException(nameof(pressureHpa), "气压必须为正，实到 " + pressureHpa);
            }

            double w = mixingRatioGPerKg / 1000d;
            double e = w * pressureHpa / (constants.Epsilon + w);
            double logRatio = Math.Log(e / constants.SaturationPressureAtZeroC);
            return constants.MagnusB * logRatio / (constants.MagnusA - logRatio);
        }

        /// <summary>位温（℃）：θ = (T+273.15)·(P₀/p)^κ。</summary>
        public static double PotentialTemperatureC(double temperatureC, double pressureHpa,
                                                   SoundingConstants constants)
        {
            if (constants == null)
            {
                throw new ArgumentNullException(nameof(constants));
            }
            if (!(pressureHpa > 0d))
            {
                throw new ArgumentOutOfRangeException(nameof(pressureHpa), "气压必须为正，实到 " + pressureHpa);
            }

            return (temperatureC + 273.15d)
                 * Math.Pow(constants.ReferencePressureHpa / pressureHpa, constants.Kappa) - 273.15d;
        }

        /// <summary>给定位温与气压的温度（℃），<see cref="PotentialTemperatureC"/> 的解析逆。</summary>
        public static double TemperatureAtPotentialTemperatureC(double potentialTemperatureC, double pressureHpa,
                                                                SoundingConstants constants)
        {
            if (constants == null)
            {
                throw new ArgumentNullException(nameof(constants));
            }
            if (!(pressureHpa > 0d))
            {
                throw new ArgumentOutOfRangeException(nameof(pressureHpa), "气压必须为正，实到 " + pressureHpa);
            }

            return (potentialTemperatureC + 273.15d)
                 * Math.Pow(pressureHpa / constants.ReferencePressureHpa, constants.Kappa) - 273.15d;
        }

        /// <summary>
        /// 伪绝热（饱和）温度直减率 dT/dp（K/hPa）。这是饱和气块沿气压变化的微分方程，
        /// 由潜热释放项与干空气热容合成，不含任何查表系数。
        /// </summary>
        public static double MoistLapseRatePerHpa(double temperatureC, double pressureHpa,
                                                  SoundingConstants constants)
        {
            if (constants == null)
            {
                throw new ArgumentNullException(nameof(constants));
            }
            if (!(pressureHpa > 0d))
            {
                throw new ArgumentOutOfRangeException(nameof(pressureHpa), "气压必须为正，实到 " + pressureHpa);
            }

            double temperatureK = temperatureC + 273.15d;
            double es = SaturationVaporPressureHpa(temperatureC, constants);
            double denominator = pressureHpa - es;
            if (denominator <= 0d)
            {
                // 饱和比湿发散：此时气块已不可能再保持饱和，退化为干绝热率。
                return constants.GasConstantDry * temperatureK
                     / (pressureHpa * constants.SpecificHeatDry);
            }

            double rs = constants.Epsilon * es / denominator;
            double latent = constants.LatentHeatVaporization;
            double numerator = constants.GasConstantDry * temperatureK
                             * (1d + latent * rs / (constants.GasConstantDry * temperatureK));
            double denominatorHeat = constants.SpecificHeatDry
                                   + latent * latent * rs * constants.Epsilon
                                     / (constants.GasConstantDry * temperatureK * temperatureK);

            if (!(denominatorHeat > 0d))
            {
                return 0d;
            }
            return numerator / (pressureHpa * denominatorHeat);
        }

        /// <summary>
        /// 湿绝热线：从参考气压（默认 1000 hPa）的湿球位温出发，沿 dT/dlnp = p·dT/dp 做 RK4 积分。
        /// 单条线只需要一次积分；采样点数由调用方给。
        /// </summary>
        public static double MoistAdiabatTemperatureC(double wetBulbPotentialTemperatureC, double pressureHpa,
                                                      SoundingConstants constants, int steps)
        {
            if (constants == null)
            {
                throw new ArgumentNullException(nameof(constants));
            }
            if (!(pressureHpa > 0d))
            {
                throw new ArgumentOutOfRangeException(nameof(pressureHpa), "气压必须为正，实到 " + pressureHpa);
            }

            double reference = constants.ReferencePressureHpa;
            if (Math.Abs(pressureHpa - reference) <= 1e-12 * reference)
            {
                return wetBulbPotentialTemperatureC;
            }
            if (steps < 4)
            {
                steps = 4;
            }

            double logFrom = Math.Log(reference);
            double logTo = Math.Log(pressureHpa);
            double h = (logTo - logFrom) / steps;
            double temperature = wetBulbPotentialTemperatureC;
            double logPressure = logFrom;

            for (int i = 0; i < steps; i++)
            {
                double k1 = DtDLogPressure(temperature, logPressure, constants);
                double k2 = DtDLogPressure(temperature + 0.5d * h * k1, logPressure + 0.5d * h, constants);
                double k3 = DtDLogPressure(temperature + 0.5d * h * k2, logPressure + 0.5d * h, constants);
                double k4 = DtDLogPressure(temperature + h * k3, logPressure + h, constants);

                temperature += h / 6d * (k1 + 2d * k2 + 2d * k3 + k4);
                logPressure += h;
            }

            return temperature;
        }

        private static double DtDLogPressure(double temperatureC, double logPressure, SoundingConstants constants)
        {
            double pressure = Math.Exp(logPressure);
            return pressure * MoistLapseRatePerHpa(temperatureC, pressure, constants);
        }

        /// <summary>
        /// 从一组（气压, 温度）层次里按 log p 线性插值出某气压处的温度。
        /// 用于特征层标注的锚点：标注点不一定正好是观测层。
        /// </summary>
        public static double TemperatureAtPressure(System.Collections.Generic.IReadOnlyList<SoundingLevel> levels,
                                                   double pressureHpa)
        {
            if (levels == null)
            {
                throw new ArgumentNullException(nameof(levels));
            }
            if (levels.Count == 0)
            {
                throw new ArgumentException("层次列表为空，无法插值。", nameof(levels));
            }

            double logTarget = Math.Log(pressureHpa);
            int lower = -1;
            int upper = -1;
            for (int i = 0; i < levels.Count; i++)
            {
                double logP = Math.Log(levels[i].PressureHpa);
                if (logP <= logTarget
                    && (lower < 0 || logP > Math.Log(levels[lower].PressureHpa)))
                {
                    lower = i;
                }
                if (logP >= logTarget
                    && (upper < 0 || logP < Math.Log(levels[upper].PressureHpa)))
                {
                    upper = i;
                }
            }

            if (lower < 0) { lower = upper; }
            if (upper < 0) { upper = lower; }
            if (lower < 0)
            {
                return levels[0].TemperatureC;
            }

            double logLower = Math.Log(levels[lower].PressureHpa);
            double logUpper = Math.Log(levels[upper].PressureHpa);
            if (Math.Abs(logUpper - logLower) < 1e-12)
            {
                return levels[lower].TemperatureC;
            }

            double t = (Math.Log(pressureHpa) - logLower) / (logUpper - logLower);
            if (t < 0d) { t = 0d; }
            if (t > 1d) { t = 1d; }
            return levels[lower].TemperatureC + t * (levels[upper].TemperatureC - levels[lower].TemperatureC);
        }
    }
}

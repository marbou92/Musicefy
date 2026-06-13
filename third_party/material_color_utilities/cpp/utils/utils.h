/*
 * Copyright 2022 Google LLC
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

#ifndef CPP_UTILS_UTILS_H_
#define CPP_UTILS_UTILS_H_

#include <cstdint>
#include <string>

namespace material_color_utilities {

using Argb = uint32_t;

struct Vec3 {
  double a = 0.0;
  double b = 0.0;
  double c = 0.0;
};

inline constexpr double kPi = 3.141592653589793;

inline constexpr double kWhitePointD65[] = {95.047, 100.0, 108.883};

int RedFromInt(const Argb argb);
int GreenFromInt(const Argb argb);
int BlueFromInt(const Argb argb);
int AlphaFromInt(const Argb argb);
Argb ArgbFromRgb(const int red, const int green, const int blue);
Argb ArgbFromLinrgb(Vec3 linrgb);
bool IsOpaque(const Argb argb);
int SanitizeDegreesInt(const int degrees);
double SanitizeDegreesDouble(const double degrees);
double DiffDegrees(const double a, const double b);
double RotationDirection(const double from, const double to);
double LstarFromArgb(const Argb argb);
std::string HexFromArgb(Argb argb);
double Linearized(const int rgb_component);
int Delinearized(const double rgb_component);
double YFromLstar(const double lstar);
double LstarFromY(const double y);
Argb IntFromLstar(const double lstar);
int Signum(double num);
double Lerp(double start, double stop, double amount);
Vec3 MatrixMultiply(Vec3 input, const double matrix[3][3]);

}  // namespace material_color_utilities
#endif  // CPP_UTILS_UTILS_H_

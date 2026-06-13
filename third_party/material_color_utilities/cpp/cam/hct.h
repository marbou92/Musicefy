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

#ifndef CPP_CAM_HCT_H_
#define CPP_CAM_HCT_H_

#include "cpp/utils/utils.h"

namespace material_color_utilities {

class Hct {
 public:
  Hct(double hue, double chroma, double tone);
  explicit Hct(Argb argb);

  double get_hue() const;
  double get_chroma() const;
  double get_tone() const;
  Argb ToInt() const;

  void set_hue(double new_hue);
  void set_chroma(double new_chroma);
  void set_tone(double new_tone);

  bool operator<(const Hct& a) const { return hue_ < a.hue_; }

 private:
  void SetInternalState(Argb argb);

  double hue_ = 0.0;
  double chroma_ = 0.0;
  double tone_ = 0.0;
  Argb argb_ = 0;
};

}  // namespace material_color_utilities

#endif  // CPP_CAM_HCT_H_

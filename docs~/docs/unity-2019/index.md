---
sidebar_position: 11
---

# Unity 2019 support

Modular Avatar has begun phasing out support for Unity 2019. While you can still use Modular Avatar on Unity 2019,
new features might not be available for Unity 2019. In a future version, support will be removed; currently, this is
likely to occur at 1.11.0.

## Differences in Unity 2019

The following features behave differently between Unity 2019 and Unity 2022 with Modular Avatar:

* MA Parameters has a new UI in 2022. You can find the [documentation for the old UI here](old-parameters.md).
  * Because of this, it is not possible to override the default value for a parameter to 0 in 2019 (0 will be treated
    as if the default value was unset). 

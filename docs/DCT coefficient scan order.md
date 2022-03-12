## Coefficient scan order

|    |    ||    |    ||    |    ||    |    |
|:--:|:--:|-|:--:|:--:|-|:--:|:--:|-|:--:|:--:|
|  0 |  1 ||  4 |  5 ||  8 |  9 || 12 | 13 | 
|  2 |  3 ||  6 |  7 || 10 | 11 || 14 | 15 | 
|
| 24 | 25 || 44 | 45 || 16 | 17 || 20 | 21 | 
| 26 | 27 || 46 | 47 || 18 | 19 || 22 | 23 | 
|
| 28 | 29 || 32 | 33 || 48 | 49 || 52 | 53 | 
| 30 | 31 || 34 | 35 || 50 | 51 || 54 | 55 | 
|
| 36 | 37 || 40 | 41 || 56 | 57 || 60 | 61 | 
| 38 | 39 || 42 | 43 || 58 | 59 || 62 | 63 | 

- Every two coefficients are stored as pair allowing for 4byte operations.
- Every four coefficients are below each other forming 4x4 Z blocks.

### Z-block order

| | | | |
|:-:|:-:|:-:|:-:|
| 0 | 1 | 2 | 3 |
| 6 | B | 4 | 5 |
| 7 | 8 | C | D |
| 9 | A | E | F |
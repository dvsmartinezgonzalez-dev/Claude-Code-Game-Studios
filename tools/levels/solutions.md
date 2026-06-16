# BoltSort QA Solution Traces

Source: `../../My project/Assets/Resources/levels.json` — 200 levels — DEV/QA ONLY.

Tube labels: `Tn` = color tube n, `Bm` = buffer/temp slot m. Each move is `from->to` (move the single top bolt).

## L1  (OK) — optimal 4, par 6

`T1->B1  T1->B1  T2->B1  T1->T2`

## L2  (OK) — optimal 5, par 7

`T1->B1  T1->B1  T2->T1  T2->B1  T2->T1`

## L3  (OK) — optimal 7, par 9

`T1->B1  T1->B1  T2->B2  T2->B1  T1->T2  T1->B1  B2->T2`

## L4  (OK) — optimal 7, par 9

`T3->B1  T3->B1  T2->T3  T2->T3  T1->T2  T1->B1  T1->T2`

## L5  (OK) — optimal 10, par 13

`T1->B1  T1->B2  T2->B1  T2->T1  T2->B2  T3->B2  T3->T1  T3->B2  T2->B1  T3->B1`

## L6  (OK) — optimal 11, par 14

`T1->B1  T1->B1  T1->B1  T2->B2  T2->B2  T2->T1  T3->B2  T3->T1  T3->B2  T2->B1  T3->T1`

## L7  (OK) — optimal 11, par 14

`T1->B1  T1->B2  T1->B1  T2->B2  T2->T1  T2->B2  T3->B1  T3->B1  T3->B2  T2->T1  T3->T1`

## L8  (OK) — optimal 14, par 18

`T1->B1  T1->B1  T3->B2  T3->B1  T3->B2  T2->T3  T4->T3  T4->B2  T4->T3  T1->T4  T2->T4  T2->T4  T1->B1  T2->B2`

## L9  (OK) — optimal 14, par 18

`T1->B1  T1->B1  T1->B2  T3->T1  T3->B1  T3->T1  T2->T3  T2->B2  T2->B1  T4->T3  T4->B2  T4->T3  T2->T1  T4->B2`

## L10  (OK) — optimal 10, par 13

`T1->B1  T1->B2  T3->B2  T2->T3  T2->T1  T3->T2  T3->T2  T3->B2  T3->B2  B1->T1`

## L11  (OK) — optimal 14, par 18

`T2->B1  T4->B1  T4->B2  T4->B2  T2->T4  T2->B2  T3->T4  T1->T3  T1->B1  T3->T1  T3->T1  T3->B1  T2->T4  T3->B2`

## L12  (OK) — optimal 14, par 18

`T1->B1  T1->B1  T1->B2  T3->B2  T4->T1  T4->B2  T4->B1  T2->T4  T2->T4  T3->T4  T3->B1  T1->T2  T1->T2  T3->B2`

## L13  (OK) — optimal 15, par 19

`T1->B1  T1->B2  T1->B2  T2->B2  T2->T1  T2->B1  T3->B1  T2->B2  T3->T2  T3->B1  T4->T2  T4->T1  T4->T1  T3->T2  T4->T2`

## L14  (OK) — optimal 19, par 24

`T1->B1  T2->B1  T2->B2  T2->B1  T4->T2  B2->T2  T1->B2  T1->B1  T4->B2  T3->T1  T3->T2  T3->B2  T3->B2  T1->T3  T1->T3  T4->T3  T4->B2  T1->B1  T4->T3`

## L15  (OK) — optimal 19, par 24

`T1->B1  T1->B2  T1->B1  T1->B2  T3->T1  T4->B2  T4->B2  T2->T3  T2->B1  T2->T1  T2->T1  T3->T2  T3->T2  T3->B2  T3->B1  T4->T2  T4->B1  T3->T2  T4->T1`

## L16  (OK) — optimal 17, par 21

`T1->B1  T1->B2  T1->B1  T2->B1  T2->T1  B2->T1  T2->B2  T3->T2  T3->T1  T3->B2  T4->T2  T4->B2  T5->B2  T5->B1  T4->T5  T3->T5  T4->T2`

## L17  (OK) — optimal 18, par 23

`T1->B1  T1->B2  T2->B1  T2->B1  T3->B1  T4->B2  T1->T2  T3->T1  T3->T1  T4->T1  T4->T3  T2->T4  T2->T4  T5->T3  T5->B2  T5->T4  T2->T3  T5->B2`

## L18  (OK) — optimal 23, par 28

`T1->B1  T1->B2  T2->B1  T3->B1  T3->B2  T4->B2  T4->B1  T3->T4  T3->B1  T2->T3  T5->T1  T5->B2  T5->T3  T2->T5  T4->T5  T4->T5  T4->T3  T1->T4  T1->T4  T1->T4  T2->T4  T1->T3  T2->B2`

## L19  (OK) — optimal 24, par 29

`T2->B1  T2->B2  T4->B1  T5->B2  T1->T5  T1->B2  T2->T4  T1->T2  T1->B1  T4->T1  T4->T1  T4->T2  T4->B2  T4->T1  T3->T4  T3->T2  T3->T4  T3->B1  T5->T4  T5->T4  T5->B2  T5->B1  T3->T1  T5->T4`

## L20  (OK) — optimal 16, par 20

`T1->B1  T1->B2  T2->T1  T2->B2  T2->B2  T2->B1  T3->T2  T3->T2  T3->B1  T3->B1  T4->B1  T4->T2  T4->B2  T4->T1  T3->B2  T4->T2`

## L21  (OK) — optimal 18, par 23

`T5->B1  T2->T5  T2->B2  T2->B1  T3->T2  T3->B1  T5->T2  T5->T2  T5->B2  T4->T5  T4->T5  T4->B2  T1->T4  T1->T5  T1->B2  T3->T4  T1->T4  T3->B1`

## L22  (OK) — optimal 24, par 29

`T4->B1  T4->B1  T5->B2  T3->T5  T3->B1  T3->B1  T3->B2  T1->T3  T2->T3  T1->T2  T1->B2  T1->B1  T5->T1  T5->T1  T5->B2  T5->T1  T2->T5  T2->T5  T2->T3  T2->T1  T4->T5  T4->T3  T2->T5  T4->B2`

## L23  (OK) — optimal 24, par 29

`T2->B1  T2->B2  T2->B2  T4->B1  T5->B2  T2->T4  T2->B1  T4->T2  T4->T2  T4->B2  T3->T4  T3->T2  T5->T2  T1->T3  T1->T4  T1->B2  T1->T2  T3->T1  T3->T1  T3->T1  T5->T1  T5->T4  T3->B1  T5->B1`

## L24  (OK) — optimal 24, par 29

`T2->B1  T5->B2  T5->B2  T5->B1  T1->T5  T1->B1  T1->B2  T4->T1  T4->B1  T2->T4  T2->B2  T1->T2  T1->T2  T3->T1  T5->T1  T5->T1  T3->T5  T3->T5  T3->T2  T4->T5  T4->T5  T4->B1  T3->B2  T4->T1`

## L25  (OK) — optimal 24, par 29

`T2->B1  T2->B1  T4->B2  T4->B1  T5->B1  T5->B2  T1->T4  T1->B1  T1->B2  T1->B2  T4->T1  T4->T1  T4->T2  T3->T4  T3->B2  T5->T4  T5->T4  T2->T5  T2->T5  T2->T4  T3->T5  T3->T5  T2->T1  T3->T1`

## L26  (OK) — optimal 21, par 26

`T3->B1  T5->B2  T5->B2  T6->B2  T6->T3  T6->B1  T2->T6  T2->B2  T2->B1  T1->T2  T3->T2  T3->T2  T1->T3  T5->T1  T3->T5  T3->T5  T4->T5  T4->T6  T4->T1  T3->T6  T4->B1`

## L27  (OK) — optimal 22, par 27

`T1->B1  T3->B1  T3->B2  T3->B1  T2->T3  T2->T3  T5->B2  T4->T1  T4->B2  T2->T4  T5->T2  T5->B1  T4->T5  T4->T5  T1->T4  T1->T4  T1->T5  T6->T2  T6->T2  T6->B2  T1->T3  T6->T4`

## L28  (OK) — optimal 25, par 30

`T1->B1  T3->B2  T5->B2  T5->T1  T5->B2  T5->B1  T1->T5  T1->T5  T1->B1  T4->T3  T4->B2  T4->T1  T4->B2  T1->T4  T1->T4  T2->T1  T2->T4  T2->T1  T2->T5  T3->T1  T3->T1  T3->B1  T3->T5  T2->T4  T3->B1`

## L29  (OK) — optimal 30, par 35

`T2->B1  T4->B1  T3->T2  T3->B1  T3->B2  T3->B2  T3->B1  T4->T3  T5->T3  T5->T3  T5->B2  T1->T5  T6->T5  T6->T1  T6->B2  T6->B1  T6->T5  T2->T6  T2->T6  T4->T6  T2->T4  T2->T3  T1->T2  T1->T2  T1->T6  T1->T3  T4->T2  T4->T2  T1->T6  T4->B2`

## L30  (OK) — optimal 23, par 28

`T2->B1  T2->B1  T5->B2  T5->B1  T5->B1  T1->T5  T1->T5  B2->T5  T1->B2  T1->B2  T3->T1  T3->T1  T3->B2  T4->B2  T4->T2  T4->B2  T4->T1  T2->T4  T2->T4  T2->T4  T3->T4  T2->B1  T3->T1`

## L31  (OK) — optimal 30, par 35

`T1->B1  T1->B1  T1->B1  T3->B2  T6->T1  T6->B2  T6->T3  T1->T6  T1->T6  T2->T1  T2->B2  T5->T6  T2->T5  T2->B1  T5->T2  T5->T2  T5->T2  T4->T5  T4->T2  T4->B2  T4->B2  T4->T1  T3->T4  T3->T4  T3->B1  T3->T4  T5->T4  T5->T4  T3->T1  T5->T1`

## L32  (OK) — optimal 30, par 35

`T1->B1  T3->B1  T3->B2  T6->T3  T6->B2  T6->B1  T6->B1  T5->T1  T5->T6  T5->B2  T5->B2  T5->B1  T1->T5  T1->T5  T1->B2  T1->T6  T3->T1  T3->T1  T3->T1  T3->T6  T2->T3  T2->T1  T2->T3  T2->T5  T4->T3  T4->T3  T4->T5  T4->T5  T2->T6  T4->T3`

## L33  (OK) — optimal 25, par 30

`T1->B1  T3->B1  T4->B2  T4->B2  T5->B1  T5->B2  T3->T1  T3->T5  T3->B2  T1->T3  T1->T3  T4->T3  T4->T3  T1->T4  T1->T4  T1->B1  T2->T1  T2->B2  T2->T4  T2->T1  T5->T1  T5->T1  T5->T1  T2->B1  T5->T4`

## L34  (OK) — optimal 30, par 35

`T1->B1  T5->B2  T5->T1  T5->B2  T5->B1  T1->T5  T1->T5  T1->B2  T2->T5  T4->T5  T1->T4  T2->T1  T2->B1  T2->T1  T3->T1  T6->T2  T6->B2  T6->B1  T6->T3  T6->B2  T3->T6  T3->T6  T3->T2  T3->T6  T4->T6  T4->T6  T4->T2  T4->B1  T3->T1  T4->T2`

## L35  (OK) — optimal 30, par 35

`T4->B1  T4->B2  T5->B1  T5->B2  T5->B1  T6->B2  T6->B1  T5->T4  T5->B2  T3->T5  T3->T5  T3->T5  T6->T5  T6->T3  T1->T6  T1->T6  T1->T4  T1->T6  T3->T1  T3->T1  T2->T3  T2->T6  T2->B1  T2->T1  T4->T3  T4->T3  T4->T3  T4->T1  T2->B2  T4->T5`

## L36  (OK) — optimal 30, par 35

`T1->B1  T2->B2  T3->B1  T4->B2  T5->B2  T5->B1  T6->B2  T3->T6  T3->B2  T3->B1  T1->T3  T2->T1  T2->T5  T2->T3  T1->T2  T1->T2  T4->T1  T4->T3  T4->T2  T1->T4  T1->T4  T5->T1  T5->T1  T5->T3  T6->T4  T6->T4  T6->T1  T6->T2  T5->B1  T6->T1`

## L37  (OK) — optimal 30, par 35

`T2->B1  T4->B1  T5->T4  T5->B2  T5->B1  T5->B2  T5->B1  T1->T5  T2->T5  T3->T5  T3->B2  T3->T2  T3->T2  T4->T3  T4->T3  T4->B2  T4->T5  T2->T4  T2->T4  T2->T4  T2->T3  T1->T2  T1->T2  T1->B2  T6->T4  T6->T3  T6->T2  T6->T2  T1->B1  T6->T5`

## L38  (OK) — optimal 31, par 36

`T5->B1  T6->B2  T6->B2  T3->T6  T3->B1  T3->B1  T3->B2  T6->T3  T6->T3  T4->T5  T4->B1  T4->T6  T4->T3  T1->T4  T2->T6  T2->B2  T2->T4  T2->B2  T6->T2  T6->T2  T6->T2  T6->T4  T1->T6  T1->T6  T1->T4  T5->T6  T5->T6  T5->T3  T5->T2  T1->B1  T5->T6`

## L39  (OK) — optimal 31, par 36

`T2->B1  T3->B1  T3->B2  T3->B2  T5->B2  T1->T2  T1->T5  T6->T1  T6->B2  T3->T1  T6->T3  T6->B1  T2->T6  T2->T6  T2->B1  T2->T3  T4->T6  T4->T3  T2->B1  T1->T2  T1->T2  T1->T2  T1->T6  T4->T1  T4->T1  T5->T1  T5->T1  T5->T2  T5->T3  T4->B2  T5->T2`

## L40  (OK) — optimal 22, par 27

`T2->B1  T2->B2  T4->B2  T4->B1  T4->B1  T1->T4  T1->B1  T1->B2  T2->T1  T3->T1  T5->T2  T5->B2  T3->T4  T3->B1  T3->T2  T4->T3  T4->T3  T4->T3  T5->T3  T5->B2  T4->T2  T5->T1`

## L41  (OK) — optimal 31, par 36

`T3->B1  T6->B2  T2->T3  T5->T2  T5->B1  T5->B2  T5->B1  T5->B2  T4->T5  T4->T5  T6->T5  T6->T5  T6->B1  T6->B2  T3->T6  T3->T6  T4->T3  T4->T6  T1->T4  T1->T6  T1->B2  T3->T4  T3->T4  T3->T5  T1->T3  T2->T3  T2->T3  T2->B1  T2->T4  T1->T6  T2->T3`

## L42  (OK) — optimal 31, par 36

`T2->B1  T2->B2  T5->B1  T5->B1  T5->B2  T3->T5  T3->B2  T3->B2  T2->T5  T2->B2  T6->T3  T6->T3  T6->B1  T6->T2  T1->T6  T3->T6  T3->T6  T3->T6  T3->T2  T1->T3  T1->T2  T1->T3  T4->T1  T4->T3  T4->T2  T4->T3  T5->T1  T5->T1  T5->T1  T4->B1  T5->T3`

## L43  (OK) — optimal 30, par 35

`T2->B1  T3->B1  T5->B2  T5->B2  T5->B1  T6->B1  T5->T6  T2->T5  T2->B2  T3->T5  T4->T5  T1->T3  T4->T1  T4->T5  T4->B1  T2->T4  T3->T2  T3->T2  T3->T4  T3->B2  T1->T3  T1->T3  T1->T4  T1->B2  T6->T3  T6->T3  T6->T2  T6->T2  T1->T3  T6->T4`

## L44  (OK) — optimal 31, par 36

`T2->B1  T3->B1  T5->B2  T5->B2  T3->T2  T3->B2  T3->B1  T2->T3  T2->T3  T1->T2  T1->T2  T1->B1  T1->B2  T5->T1  T5->T3  T6->T1  T5->B1  T2->T5  T2->T5  T2->T5  T6->T5  T6->T2  T6->T1  T2->T6  T2->T6  T4->T6  T4->B2  T4->T1  T4->T6  T2->T5  T4->T3`

## L45  (OK) — optimal 31, par 36

`T1->B1  T2->B2  T4->B1  T4->T1  T4->B2  T4->T2  T6->T4  T6->B1  T5->T6  T5->B1  T5->B2  T5->T6  T5->B2  T2->T5  T2->T5  T3->T5  T3->T5  T3->T4  T3->B1  T6->T3  T6->T3  T6->T3  T6->B2  T1->T6  T1->T6  T1->T6  T1->T5  T2->T6  T1->T3  T2->T4  T2->T4`

## L46  (OK) — optimal 31, par 36

`T2->B1  T3->B2  T6->B2  T6->T2  T6->B2  T3->T6  T3->B1  T3->B1  T3->B2  T4->T3  T6->T3  T6->T3  T1->T6  T1->T3  T1->T6  T2->T6  T2->T6  T5->T2  T5->T1  T5->B1  T5->B2  T1->T5  T1->T5  T2->T1  T2->T1  T2->T5  T4->T1  T4->T5  T4->T1  T2->B1  T4->T3`

## L47  (OK) — optimal 31, par 36

`T1->B1  T2->B1  T4->B2  T5->B1  T5->B2  T2->T1  T5->T4  T5->T2  T5->B1  T2->T5  T2->T5  T2->B2  T6->T2  T6->B2  T6->T5  T6->T5  T1->T6  T1->T6  T3->T1  T3->T6  T3->T5  T3->T2  T1->T3  T1->T3  T1->T6  T4->T3  T4->T3  T4->T2  T4->T2  T1->B2  T4->B1`

## L48  (OK) — optimal 30, par 35

`T1->B1  T3->B2  T5->B2  T5->B2  T4->T1  T4->B1  T5->T4  T5->B1  T6->T5  T6->B2  T2->T4  T2->B1  T2->B2  T2->T5  T1->T2  T1->T2  T1->B1  T6->T1  T6->T2  T4->T6  T4->T6  T4->T6  T4->T5  T1->T4  T1->T4  T3->T4  T3->T6  T3->T4  T1->T2  T3->T5`

## L49  (OK) — optimal 32, par 37

`T4->B1  T4->B2  T5->B2  T5->B2  T6->B1  T2->T4  T2->T6  T3->T2  T3->B2  T3->B1  T3->T5  T4->T3  T4->T3  T4->T3  T1->T4  T1->T3  T2->T4  T2->T4  T2->T1  T6->T2  T6->T2  T6->T2  T6->B2  T6->B1  T1->T6  T1->T6  T1->B1  T5->T6  T5->T6  T5->T6  T1->T4  T5->T2`

## L50  (OK) — optimal 32, par 37

`T2->B1  T2->B1  T2->B2  T3->B1  T3->B1  T4->B2  T4->B2  T5->T4  T5->B2  T2->T5  T2->T3  T1->T2  T1->T2  T1->T5  T1->T2  T4->T1  T4->T1  T4->B1  T6->T1  T6->T2  T6->T1  T4->B2  T5->T4  T5->T4  T5->T4  T5->T2  T3->T5  T3->T5  T3->T4  T6->T4  T3->T5  T6->T5`

## L51  (OK) — optimal 35, par 40

`T2->B1  T4->B1  T5->B1  T5->B1  T5->B1  T1->T5  T2->T1  T2->T5  T6->T5  T6->T2  T6->T4  T3->T6  T3->T2  T3->T2  T3->T6  T4->T3  T4->T3  T4->T6  T1->T4  T1->T4  T1->T4  T1->T3  T4->T1  T4->T1  T4->T1  T4->T1  T2->T4  T2->T4  T2->T4  T2->T4  T5->T2  T5->T2  T5->T2  T5->T2  T5->T3`

## L52  (OK) — optimal 34, par 39

`T3->B1  T3->B1  T3->B1  T5->B1  T1->T3  T4->T5  T6->T3  T6->T4  T6->T3  T1->T6  T2->T6  T1->T2  T1->B1  T1->T6  T2->T1  T2->T1  T2->T1  T4->T1  T4->T1  T4->T2  T4->T2  T3->T4  T3->T4  T3->T4  T3->T4  T6->T3  T6->T3  T6->T3  T6->T3  T5->T6  T5->T6  T5->T6  T5->T6  T5->T2`

## L53  (OK) — optimal 37, par 42

`T2->B1  T6->B1  T4->T2  T1->T4  T6->T1  B1->T6  B1->T6  T2->B1  T2->B1  T4->T2  T4->T2  T5->T4  T5->B1  T5->B1  T4->T5  T4->T5  T3->T4  T3->T4  T3->T4  T2->T3  T2->T3  T2->T3  T2->B1  T1->T2  T1->T2  T1->T2  T1->T5  T1->T2  T4->T1  T4->T1  T4->T1  T4->T1  T6->T4  T6->T4  T6->T4  T6->T4  T6->T1`

## L54  (OK) — optimal 36, par 41

`T1->B1  T1->B1  T2->B1  T2->B1  T4->B1  T6->T2  T1->T6  T3->T1  T3->T4  T5->T1  T5->T2  T3->T5  T1->T3  T1->T3  T1->T3  T2->T1  T2->T1  T2->T1  T2->T5  T6->T2  T6->T2  T6->T1  T6->T2  T5->T6  T5->T6  T5->T6  T5->T6  T4->T5  T4->T5  T4->T2  T4->T5  T3->T4  T3->T4  T3->T4  T3->T4  T3->T5`

## L55  (OK) — optimal 40, par 45

`T1->B1  T2->B1  T2->B1  T2->B1  T1->T2  T4->T1  T3->T4  T5->T3  T5->T2  T6->T5  T6->T1  T5->T6  T5->T6  T4->T5  T4->T5  T4->T5  T1->T4  T1->T4  T1->T4  T3->T1  T3->T1  T3->T1  T3->B1  T1->T3  T1->T3  T1->T3  T1->T3  T4->T1  T4->T1  T4->T1  T4->T1  T5->T4  T5->T4  T5->T4  T5->T4  T6->T5  T6->T5  T6->T5  T6->T5  T6->T2`

## L56  (OK) — optimal 40, par 45

`T4->B1  T5->B1  T3->T4  T1->T3  T2->T1  T5->T2  B1->T5  B1->T5  T1->B1  T1->B1  T6->B1  T1->T6  T2->T1  T2->T1  T2->T1  T3->T2  T3->T2  T3->B1  T4->T3  T4->T3  T4->T2  T5->T4  T5->T4  T5->T4  T3->T5  T3->T5  T3->T5  T4->T3  T4->T3  T4->T3  T4->T3  T2->T4  T2->T4  T2->T4  T2->T4  T6->T2  T6->T2  T6->T2  T6->B1  T6->T2`

## L57  (OK) — optimal 44, par 49

`T1->B1  T5->B1  T5->B1  T1->T5  T3->T1  T3->B1  T2->T1  T4->T3  T6->T5  T6->T3  T2->T6  T2->T4  T5->T2  T5->T2  T5->T2  T5->T6  T5->B1  T1->T5  T1->T5  T1->T5  T1->T5  T6->T1  T6->T1  T6->T1  T4->T6  T4->T6  T4->T6  T3->T4  T3->T4  T3->T4  T3->T5  T2->T3  T2->T3  T2->T3  T2->T3  T4->T2  T4->T2  T4->T2  T4->T2  T6->T4  T6->T4  T6->T4  T6->T4  T6->T1`

## L58  (OK) — optimal 39, par 44

`T4->B1  T6->B1  T2->T6  T2->B1  T5->T4  T3->T5  T2->T3  T2->B1  T2->B1  T5->T2  T5->T2  T6->T5  T6->T5  T6->T2  T1->T6  T1->T2  T1->T2  T3->T6  T3->T6  T4->T1  T4->T1  T3->T4  T1->T3  T1->T3  T1->T3  T5->T1  T5->T1  T5->T1  T5->T4  T5->T1  T6->T5  T6->T5  T6->T5  T6->T5  T4->T6  T4->T6  T4->T6  T4->T5  T4->T6`

## L59  (OK) — optimal 45, par 50

`T1->B1  T6->B1  T5->T6  T5->B1  T4->T1  T5->T4  T1->T5  T1->T5  T1->T5  T6->T1  T6->T1  T2->T6  T3->T2  T3->T1  T2->T3  T2->T3  T4->T2  T4->T2  T4->B1  T3->T4  T3->T4  T3->T4  T3->B1  T4->T3  T4->T3  T4->T3  T4->T3  T5->T4  T5->T4  T5->T4  T5->T4  T5->T6  T2->T5  T2->T5  T2->T5  T2->T5  T1->T2  T1->T2  T1->T2  T1->T2  T6->T1  T6->T1  T6->T1  T6->T5  T6->T1`

## L60  (OK) — optimal 33, par 38

`T4->B1  T4->B1  T4->B1  T2->T4  T3->T4  T3->T4  T6->T3  T1->T6  T1->T3  T1->B1  T6->T1  T6->T1  T5->T6  T5->T2  T5->T1  T5->T6  T1->T5  T1->T5  T1->T5  T1->T5  T1->B1  T3->T1  T3->T1  T3->T1  T2->T3  T2->T3  T2->T1  T2->T1  T6->T2  T6->T2  T6->T2  T6->T2  T6->T3`

## L61  (OK) — optimal 39, par 44

`T2->B1  T3->B2  T3->B2  T5->B2  T5->B1  T4->T5  T4->B1  T1->T4  T1->T5  T2->T4  T6->T3  T2->T6  T2->T1  T2->T3  T1->T2  T1->T2  B1->T2  B1->T2  B1->T2  T1->B1  T3->T1  T3->T1  T3->T1  T3->T1  T3->B1  T4->T3  T4->T3  T4->T3  T4->T3  T4->B1  T6->T4  T6->T4  T6->T3  B1->T6  B1->T6  B1->T6  B2->T4  B2->T4  B2->T4`

## L62  (OK) — optimal 43, par 48

`T2->B1  T2->B1  T3->B2  T5->B2  T1->T3  T1->B1  T4->T5  T4->B2  T3->T1  T3->T1  T4->T2  T5->T4  T5->T4  T5->T3  T5->T2  T5->T3  T4->T5  T4->T5  T4->T5  T2->T4  T2->T4  T2->T4  T2->T5  T3->T2  T3->T2  T3->T2  T3->T4  B1->T3  B1->T3  B1->T3  T6->B1  T6->B1  T6->T2  T1->T6  T1->T6  T1->T6  T1->T5  T1->T3  B1->T1  B1->T1  B2->T1  B2->T1  B2->T1`

## L63  (OK) — optimal 40, par 45

`T1->B1  T4->B1  T6->B2  T2->T6  T3->T1  T3->T2  T3->T4  T3->B1  T3->B2  T4->T3  T4->T3  T4->T3  T4->B2  T4->T3  T1->T4  T1->T4  T1->T4  T1->T3  B2->T1  B2->T1  B2->T1  T5->B2  T5->T4  T6->T5  T6->T5  T6->T4  T2->T6  T2->T6  T2->T1  T2->B2  T5->T2  T5->T2  T5->T2  T5->T2  T5->T6  B1->T5  B1->T5  B1->T5  B2->T5  B2->T5`

## L64  (OK) — optimal 43, par 48

`T1->B1  T1->B2  T2->B1  T4->B2  T5->B2  T5->T1  T5->B1  T2->T5  T6->T5  T6->T4  T6->T2  T6->T2  T6->T1  T2->T6  T2->T6  T2->T6  T3->T2  T3->T5  T4->T2  T4->T2  T4->T6  T4->T3  T1->T4  T1->T4  T1->T4  T1->T6  T1->T4  T2->T1  T2->T1  T2->T1  T2->T1  B1->T2  B1->T2  B1->T2  T3->B1  T3->B1  T3->T1  T3->T2  B1->T3  B1->T3  B2->T3  B2->T3  B2->T3`

## L65  (OK) — optimal 47, par 52

`T2->B1  T6->B1  T2->T6  T2->B1  T2->B2  T2->B2  T3->T2  T4->T2  T6->T2  T6->T2  T3->T6  T3->T2  T1->T4  T1->T3  T1->T3  T1->B2  T5->T3  T1->T5  T3->T1  T3->T1  T3->T1  T3->T1  B2->T3  B2->T3  B2->T3  T4->B2  T4->B2  T4->T1  T5->T4  T5->T4  T5->T3  T5->B2  T4->T5  T4->T5  T4->T5  B2->T4  B2->T4  B2->T4  T6->B2  T6->B2  T6->T4  T6->T5  B1->T6  B1->T6  B1->T6  B2->T6  B2->T6`

## L66  (OK) — optimal 42, par 47

`T1->B1  T4->B1  T5->B1  T3->T4  T5->T3  T5->B2  T5->T1  T5->B2  T2->T5  T2->T5  T3->T5  T3->T5  T2->T3  T1->T2  T1->T2  T3->T1  T3->T1  T3->T2  T1->T3  T1->T3  T1->T3  B2->T1  B2->T1  T6->B2  T6->B2  T6->T5  T6->T3  T6->T1  T4->T6  T4->T6  B2->T6  B2->T6  T4->B2  T4->T6  T2->T4  T2->T4  T2->T4  T2->T4  B1->T2  B1->T2  B1->T2  B2->T2`

## L67  (OK) — optimal 41, par 46

`T1->B1  T2->B1  T6->B1  T6->T1  T6->B2  T2->T6  T3->T6  T5->T2  T5->T3  T5->T2  T5->T6  T4->T5  T4->B2  T4->T5  T4->T5  T2->T4  T2->T4  T2->T4  T2->T5  T2->B2  T1->T2  T1->T2  T1->T2  T1->T2  B2->T1  B2->T1  B2->T1  T3->B2  T3->B2  T3->T4  T3->T2  T6->T3  T6->T3  T6->T3  T6->T3  T6->T1  B1->T6  B1->T6  B1->T6  B2->T6  B2->T6`

## L68  (OK) — optimal 40, par 45

`T1->B1  T4->B2  T6->B2  T6->B2  T2->T1  T3->T2  T3->B1  T1->T3  T1->T3  T1->B1  T1->T6  T1->T4  T2->T1  T2->T1  T2->T1  T2->T6  T2->T1  T6->T2  T6->T2  T6->T2  T6->T1  T3->T6  T3->T6  T3->T6  T3->T2  T4->T3  T4->T3  T4->T2  T4->T6  T5->T4  B1->T4  B1->T4  B1->T4  T5->B1  T5->T3  T5->T3  B1->T5  B2->T5  B2->T5  B2->T5`

## L69  (OK) — optimal 45, par 50

`T1->B1  T4->B2  T4->B1  T5->B1  T5->B2  T5->B2  T5->T4  T5->T1  T6->T5  T6->T4  T1->T6  T1->T6  T1->T5  B2->T5  B2->T5  B2->T5  T1->B2  B1->T1  B1->T1  B1->T1  T2->B1  T2->B2  T2->B1  T2->B1  T2->T1  T4->T2  T4->T2  T4->T2  T4->T2  T3->T4  B2->T4  B2->T4  T3->B2  T3->B2  T3->T2  T6->T3  T6->T3  T6->T3  T6->T4  T6->T3  B1->T6  B1->T6  B1->T6  B2->T6  B2->T6`

## L70  (OK) — optimal 32, par 37

`T6->B1  T6->B2  T6->B1  T1->T6  T3->T6  T5->T1  T5->T3  T5->T6  B2->T5  T3->B2  T3->B2  T3->B1  T3->T5  T1->T3  T1->T3  T1->B2  T1->T5  T2->T3  T4->T3  T4->T1  T4->T1  T4->T1  B1->T4  B1->T4  B1->T4  T2->B1  T2->T4  T2->T1  B1->T2  B2->T2  B2->T2  B2->T2`

## L71  (OK) — optimal 34, par 39

`T1->B1  T6->B2  T5->T6  T4->T5  T1->T4  T5->T1  T5->T1  T3->T5  T3->B1  T3->B2  T3->T5  T1->T3  T1->T3  T1->T3  T1->T3  T2->T1  B2->T1  B2->T1  T2->B2  T4->T2  T4->T2  T4->B2  T6->T4  T6->T4  T6->T1  T6->T4  T2->T6  T2->T6  T2->T6  T2->T6  B1->T2  B1->T2  B2->T2  B2->T2`

## L72  (OK) — optimal 42, par 47

`T1->B1  T1->B1  T1->B2  T2->B2  T2->T1  T5->T1  T2->T5  T2->T1  T6->T2  B1->T2  B1->T2  T3->B1  T3->T2  T4->B1  T4->T3  T6->T4  T5->T6  T5->T6  T5->T3  T6->T5  T6->T5  T6->T5  B1->T6  B1->T6  T4->B1  T4->B1  T3->T4  T3->T4  T3->T4  T3->T6  T5->T3  T5->T3  T5->T3  T5->T3  T1->T5  T1->T5  T1->T5  T1->T5  B1->T1  B1->T1  B2->T1  B2->T1`

## L73  (OK) — optimal 46, par 51

`T1->B1  T6->B2  T3->T6  T3->B2  T3->T1  T3->B1  T2->T3  T5->T3  B2->T3  B2->T3  T1->B2  T1->B2  T1->T2  T1->T5  T4->T1  T6->T1  T6->T1  T6->T4  B1->T6  B1->T6  T4->B1  T4->B1  T4->T1  T2->T4  T2->T4  T2->T4  T5->T2  T5->T2  T5->T2  T5->T6  T4->T5  T4->T5  T4->T5  T4->T5  T6->T4  T6->T4  T6->T4  T6->T4  T2->T6  T2->T6  T2->T6  T2->T6  B1->T2  B1->T2  B2->T2  B2->T2`

## L74  (OK) — optimal 44, par 49

`T4->B1  T4->B2  T6->B2  T6->B1  T5->T6  T1->T5  T4->T1  T2->T4  B1->T4  B1->T4  T2->B1  T2->T6  T2->B1  T6->T2  T6->T2  T6->T2  T5->T6  T5->T6  T5->T2  B2->T5  B2->T5  T1->B2  T1->B2  T3->T5  T1->T3  T1->T6  T3->T1  T3->T1  B2->T1  B2->T1  T3->B2  T3->B2  T5->T3  T5->T3  T5->T3  T5->T3  T4->T5  T4->T5  T4->T5  T4->T5  B1->T4  B1->T4  B2->T4  B2->T4`

## L75  (OK) — optimal 40, par 45

`T3->B1  T3->B2  T5->B1  T6->B2  T2->T3  T5->T2  T6->T3  T1->T6  T5->T1  T5->T6  T2->T5  T2->T5  T1->T2  T1->T2  B2->T1  B2->T1  T4->B2  T4->B2  T4->T5  T2->T4  T2->T4  T2->T4  T1->T2  T1->T2  T1->T2  T6->T1  T6->T1  T6->T1  T3->T6  T3->T6  T3->T6  T3->T5  T6->T3  T6->T3  T6->T3  T6->T3  B1->T6  B1->T6  B2->T6  B2->T6`

## L76  (OK) — optimal 44, par 49

`T2->B1  T4->B1  T6->B2  T1->T4  T3->T2  T3->T6  T2->T3  T2->T3  T2->T1  T2->B2  T6->T2  T6->T2  T5->T6  T5->T2  T6->T5  T6->T5  T4->T6  T4->T6  T4->T2  T3->T4  T3->T4  T3->T4  T1->T3  T1->T3  T1->T6  T3->T1  T3->T1  T3->T1  T6->T3  T6->T3  T6->T3  T6->T3  T1->T6  T1->T6  T1->T6  T1->T6  T5->T1  T5->T1  T5->T1  T5->T1  B1->T5  B1->T5  B2->T5  B2->T5`

## L77  (OK) — optimal 44, par 49

`T1->B1  T2->B2  T2->B2  T3->T2  T4->B1  T4->T1  T4->T2  T3->T4  T1->T3  T1->T3  T1->T4  T5->T1  T6->T5  T6->T4  B2->T1  B2->T1  T6->B2  T5->T6  T5->T6  T5->B2  T3->T5  T3->T5  T3->T5  T3->T6  T4->T3  T4->T3  T4->T3  T4->T3  T1->T4  T1->T4  T1->T4  T1->T4  T6->T1  T6->T1  T6->T1  T6->T1  T5->T6  T5->T6  T5->T6  T5->T6  B1->T5  B1->T5  B2->T5  B2->T5`

## L78  (OK) — optimal 46, par 51

`T1->B1  T2->B2  T6->B1  T1->T6  T1->B2  T6->T1  T6->T1  T6->T2  T6->T1  T2->T6  T2->T6  T3->T6  T3->T6  B2->T3  B2->T3  T2->B2  T2->B2  T5->T2  B1->T2  B1->T2  T5->B1  T4->T5  T4->T5  T4->T2  T5->T4  T5->T4  T5->T4  B1->T5  T1->B1  T1->B1  B2->T5  B2->T5  T1->B2  T1->B2  T3->T1  T3->T1  T3->T1  T3->T1  T4->T3  T4->T3  T4->T3  T4->T3  B1->T4  B1->T4  B2->T4  B2->T4`

## L79  (OK) — optimal 43, par 48

`T2->B1  T3->B2  T4->B1  T5->B2  T5->T3  T6->T5  T2->T6  T5->T2  T5->T2  T4->T5  B2->T5  B2->T5  T4->B2  T6->T4  T6->T4  T6->B2  T2->T6  T2->T6  T2->T6  T4->T2  T4->T2  T4->T2  T1->T4  B1->T4  B1->T4  T1->B1  T1->B1  T3->T1  T3->T1  T3->T4  T3->T1  T5->T3  T5->T3  T5->T3  T5->T3  T6->T5  T6->T5  T6->T5  T6->T5  B1->T6  B1->T6  B2->T6  B2->T6`

## L80  (OK) — optimal 43, par 48

`T2->B1  T2->B2  T6->B2  T2->T6  T2->B1  T6->T2  T6->T2  T4->T6  T4->T2  B2->T4  B2->T4  T5->B2  T1->T6  T1->T5  T1->T2  T6->T1  T6->T1  T6->T1  T6->B2  T5->T6  T5->T6  B1->T6  B1->T6  T3->B1  T3->B1  T3->T5  B2->T3  B2->T3  T5->B2  T5->B2  T5->T3  T1->T5  T1->T5  T1->T5  T1->T5  T3->T1  T3->T1  T3->T1  T3->T1  B1->T3  B1->T3  B2->T3  B2->T3`

## L81  (OK) — optimal 45, par 50

`T5->B1  T1->T5  T6->T1  T3->T6  T3->B1  T7->T3  T7->B1  T2->T7  T2->B1  T5->T2  T5->T2  T6->T5  T6->T5  T4->T6  T4->T3  T4->T6  T4->T7  T4->B1  T2->T4  T2->T4  T2->T4  T1->T2  T1->T2  T1->T4  T6->T1  T6->T1  T6->T1  T5->T6  T5->T6  T5->T6  T5->T2  T5->T4  T6->T5  T6->T5  T6->T5  T6->T5  T2->T6  T2->T6  T2->T6  T2->T6  T7->T2  T7->T2  T7->T2  T7->T5  T7->T2`

## L82  (OK) — optimal 48, par 53

`T2->B1  T3->B1  T3->B1  T3->B1  T5->T2  T5->B1  T2->T5  T2->T5  T4->T3  T1->T4  T1->T3  T4->T1  T4->T1  T4->T2  T1->T4  T1->T4  T1->T4  T1->T3  T1->T2  T3->T1  T3->T1  T3->T1  T3->T1  T5->T3  T5->T3  T5->T3  T5->T1  T7->T5  T7->T3  T5->T7  T5->T7  T4->T5  T4->T5  T4->T5  T4->T5  T7->T4  T7->T4  T7->T4  T2->T7  T2->T7  T2->T7  T6->T2  T6->T2  T6->T5  T2->T6  T2->T6  T2->T6  T2->T4`

## L83  (OK) — optimal 48, par 53

`T5->B1  T7->B1  T6->T5  T6->B1  T5->T6  T5->T6  T2->T5  T2->B1  T1->T5  T2->T1  T4->T2  T4->B1  T7->T2  T7->T4  T5->T7  T5->T7  T5->T7  T5->T2  T5->T4  T2->T5  T2->T5  T2->T5  T2->T5  T1->T2  T1->T2  T3->T1  T3->T1  T3->T5  T6->T3  T6->T3  T6->T3  T1->T6  T1->T6  T1->T6  T4->T1  T4->T1  T4->T1  T4->T2  T4->T2  T1->T4  T1->T4  T1->T4  T1->T4  T6->T1  T6->T1  T6->T1  T6->T1  T6->T4`

## L84  (OK) — optimal 46, par 51

`T3->B1  T4->B1  T5->B1  T1->T3  T1->B1  T2->T1  T6->T5  T2->T6  T7->T1  T2->T7  T6->T2  T6->T2  T6->T2  T3->T6  T3->T6  T3->T4  T3->B1  T2->T3  T2->T3  T2->T3  T2->T3  T2->T6  T5->T2  T5->T2  T7->T2  T7->T2  T4->T7  T4->T7  T5->T4  T1->T5  T1->T5  T1->T5  T1->T4  T1->T2  T6->T1  T6->T1  T6->T1  T6->T1  T4->T6  T4->T6  T4->T6  T7->T4  T7->T4  T7->T4  T7->T1  T7->T6`

## L85  (OK) — optimal 51, par 56

`T3->B1  T5->B1  T6->B1  T6->B1  T2->T5  T7->T3  T1->T7  T6->T1  T2->T6  T4->T2  T4->T6  T5->T4  T5->T4  T3->T5  T3->T5  T3->T6  T4->T3  T4->T3  T4->T3  T4->B1  T1->T4  T1->T4  T7->T1  T7->T1  T7->T2  T7->T4  T7->T4  T6->T7  T6->T7  T6->T7  T6->T7  T5->T6  T5->T6  T5->T6  T5->T6  T3->T5  T3->T5  T3->T5  T3->T5  T1->T3  T1->T3  T1->T3  T2->T1  T2->T1  T2->T1  T2->T3  T1->T2  T1->T2  T1->T2  T1->T2  T1->T7`

## L86  (OK) — optimal 51, par 56

`T1->B1  T3->B1  T3->B1  T6->B1  T5->T6  T5->B1  T4->T3  T7->T4  T7->T3  T4->T7  T4->T7  T1->T4  T5->T1  T2->T5  T6->T5  T6->T5  T6->T2  T6->T1  T4->T6  T4->T6  T4->T6  T1->T4  T1->T4  T1->T4  T1->T6  T2->T1  T2->T1  T2->T4  T7->T2  T7->T2  T7->T2  T1->T7  T1->T7  T1->T7  T2->T1  T2->T1  T2->T1  T2->T1  T7->T2  T7->T2  T7->T2  T7->T2  T5->T7  T5->T7  T5->T7  T5->T7  T3->T5  T3->T5  T3->T5  T3->T1  T3->T5`

## L87  (OK) — optimal 51, par 56

`T6->B1  T6->B1  T1->T6  T5->T1  T4->T5  T4->B1  T3->T4  T3->B1  T2->T6  T7->T4  T7->T2  T7->T3  T7->B1  T5->T7  T5->T7  T1->T5  T1->T5  T3->T1  T3->T1  T6->T3  T6->T3  T6->T3  T6->T7  T4->T6  T4->T6  T4->T6  T2->T4  T2->T4  T2->T4  T2->T7  T5->T2  T5->T2  T5->T2  T1->T5  T1->T5  T1->T5  T1->T6  T3->T1  T3->T1  T3->T1  T3->T1  T3->T2  T4->T3  T4->T3  T4->T3  T4->T3  T5->T4  T5->T4  T5->T4  T5->T4  T5->T3`

## L88  (OK) — optimal 50, par 55

`T2->B1  T7->B1  T6->T7  T1->T6  T1->B1  T5->T1  T5->B1  T2->T1  T2->T5  T2->B1  T7->T2  T7->T2  T4->T7  T4->T7  T3->T4  T3->T5  T3->T2  T6->T3  T6->T3  T6->T2  T7->T6  T7->T6  T7->T6  T5->T7  T5->T7  T5->T7  T1->T5  T1->T5  T1->T5  T1->T3  T6->T1  T6->T1  T6->T1  T6->T1  T5->T6  T5->T6  T5->T6  T5->T6  T4->T5  T4->T5  T4->T5  T3->T4  T3->T4  T3->T4  T3->T4  T7->T3  T7->T3  T7->T3  T7->T3  T7->T5`

## L89  (OK) — optimal 54, par 59

`T3->B1  T5->B1  T7->B1  T5->T7  T5->B1  T2->T5  T2->T5  T1->T2  T6->T5  T6->T2  T3->T6  T3->T1  T6->T3  T6->T3  T6->T3  T6->B1  T1->T6  T1->T6  T4->T6  T7->T1  T7->T1  T7->T6  T7->T4  T7->T6  T5->T7  T5->T7  T5->T7  T5->T7  T4->T5  T4->T5  T4->T7  T1->T4  T1->T4  T1->T4  T5->T1  T5->T1  T5->T1  T3->T5  T3->T5  T3->T5  T3->T5  T2->T3  T2->T3  T2->T3  T2->T5  T1->T2  T1->T2  T1->T2  T1->T2  T4->T1  T4->T1  T4->T1  T4->T1  T4->T3`

## L90  (OK) — optimal 53, par 58

`T1->B1  T5->B1  T5->B1  T4->T5  T4->B1  T4->B1  T3->T5  T7->T3  T4->T7  T6->T4  T6->T4  T3->T6  T3->T6  T7->T3  T7->T3  T1->T7  T1->T4  T2->T1  T2->T4  T7->T1  T7->T1  T5->T7  T5->T7  T5->T7  T3->T5  T3->T5  T3->T5  T6->T3  T6->T3  T6->T3  T6->T2  T6->T2  T7->T6  T7->T6  T7->T6  T7->T6  T5->T7  T5->T7  T5->T7  T5->T7  T2->T5  T2->T5  T2->T5  T2->T6  T3->T2  T3->T2  T3->T2  T3->T2  T1->T3  T1->T3  T1->T3  T1->T3  T1->T5`

## L91  (OK) — optimal 53, par 58

`T3->B1  T6->B1  T7->T6  T7->T3  B1->T7  B1->T7  T1->B1  T3->B1  T3->B1  T4->T3  T1->T4  T1->B1  T4->T1  T4->T1  T4->T3  T4->B1  T5->T4  T2->T5  T2->T4  T6->T2  T6->T2  T5->T6  T5->T6  T5->T1  T7->T5  T7->T5  T7->T5  T6->T7  T6->T7  T6->T7  T4->T6  T4->T6  T4->T6  T5->T4  T5->T4  T5->T4  T5->T4  T7->T5  T7->T5  T7->T5  T7->T5  T6->T7  T6->T7  T6->T7  T6->T7  T3->T6  T3->T6  T3->T6  T2->T3  T2->T3  T2->T3  T2->T4  T2->T6`

## L92  (OK) — optimal 44, par 49

`T4->B1  T5->B1  T6->B1  T6->B1  T3->T6  T4->T3  T2->T4  T2->T6  T5->T2  T7->T2  T1->T7  T1->T4  T7->T1  T7->T1  T5->T7  T5->T7  T2->T5  T2->T5  T2->T5  T2->T5  T2->B1  T4->T2  T4->T2  T4->T2  T3->T4  T3->T4  T3->T2  T6->T3  T6->T3  T6->T3  T6->T4  T3->T6  T3->T6  T3->T6  T3->T6  T1->T3  T1->T3  T1->T3  T1->T2  T7->T1  T7->T1  T7->T1  T7->T3  T7->T1`

## L93  (OK) — optimal 47, par 52

`T1->B1  T5->B1  T6->B1  T6->B1  T1->T6  T2->T5  T1->T2  T3->T1  T3->B1  T3->T6  T4->T3  T5->T1  T5->T1  T2->T5  T2->T5  T2->T3  T2->T3  T2->T4  T5->T2  T5->T2  T5->T2  T7->T5  T7->T2  T4->T7  T4->T7  T4->T2  T4->T5  T6->T4  T6->T4  T6->T4  T7->T6  T7->T6  T7->T6  T7->T5  T6->T7  T6->T7  T6->T7  T6->T7  T1->T6  T1->T6  T1->T6  T1->T6  T3->T1  T3->T1  T3->T1  T3->T1  T3->T4`

## L94  (OK) — optimal 47, par 52

`T4->B1  T1->T4  T5->T1  T5->B1  T1->T5  T1->T5  T3->T1  T3->B1  T4->T3  T4->T3  T4->B1  T6->T1  T6->T4  T6->B1  T7->T4  T6->T7  T6->T4  T3->T6  T3->T6  T3->T6  T7->T6  T7->T6  T5->T3  T5->T3  T5->T3  T5->T7  T5->T7  T3->T5  T3->T5  T3->T5  T3->T5  T1->T3  T1->T3  T1->T3  T2->T1  T2->T3  T2->T1  T2->T5  T7->T2  T7->T2  T7->T2  T7->T2  T4->T7  T4->T7  T4->T7  T4->T7  T4->T1`

## L95  (OK) — optimal 47, par 52

`T3->B1  T3->B1  T6->B1  T3->T6  B1->T3  B1->T3  B1->T3  T2->B1  T5->B1  T6->B1  T6->B1  T1->T6  T2->T6  T5->T2  T7->T1  T7->T5  T7->B1  T1->T7  T1->T7  T5->T1  T5->T1  T2->T5  T2->T5  T2->T7  T4->T5  T2->T4  T6->T2  T6->T2  T6->T2  T6->T2  T5->T6  T5->T6  T5->T6  T5->T6  T1->T5  T1->T5  T1->T5  T1->T2  T4->T1  T4->T1  T4->T1  T4->T1  T3->T4  T3->T4  T3->T4  T3->T4  T3->T5`

## L96  (OK) — optimal 49, par 54

`T1->B1  T2->B1  T3->B1  T4->B1  T6->T3  T6->T4  T1->T6  T7->T1  T5->T7  T5->T6  T5->B1  T5->T1  T5->T2  T3->T5  T3->T5  T3->T5  T4->T5  T4->T5  T2->T4  T2->T4  T7->T2  T7->T2  T7->T3  T7->T3  T2->T7  T2->T7  T2->T7  T1->T2  T1->T2  T1->T2  T1->T7  T1->T3  T4->T1  T4->T1  T4->T1  T4->T1  T3->T4  T3->T4  T3->T4  T3->T4  T2->T3  T2->T3  T2->T3  T2->T3  T6->T2  T6->T2  T6->T2  T6->T1  T6->T2`

## L97  (OK) — optimal 48, par 53

`T3->B1  T5->B1  T6->B1  T3->T6  T7->T3  T7->T5  T7->B1  T1->T3  T1->T7  T6->T1  T6->T1  T6->T7  T5->T6  T5->T6  T5->B1  T4->T6  T4->T5  T4->T5  T1->T4  T1->T4  T1->T4  T3->T1  T3->T1  T3->T1  T3->T7  T3->T5  T1->T3  T1->T3  T1->T3  T1->T3  T2->T3  T6->T1  T6->T1  T6->T1  T6->T1  T4->T6  T4->T6  T4->T6  T4->T6  T5->T4  T5->T4  T5->T4  T5->T4  T7->T5  T7->T5  T7->T5  T7->T5  T7->T2`

## L98  (OK) — optimal 50, par 55

`T1->B1  T3->B1  T6->B1  T6->T1  T6->T3  T2->T6  T4->T6  T5->T4  T7->T5  T7->B1  T2->T7  T4->T2  T4->T2  T5->T4  T5->T4  T5->T6  T3->T5  T3->T5  T7->T3  T7->T3  T7->B1  T1->T7  T1->T7  T1->T7  T1->T5  T1->T7  T5->T1  T5->T1  T5->T1  T5->T1  T4->T5  T4->T5  T4->T5  T3->T4  T3->T4  T3->T4  T2->T3  T2->T3  T2->T3  T2->T1  T2->T5  T3->T2  T3->T2  T3->T2  T3->T2  T4->T3  T4->T3  T4->T3  T4->T3  T4->T2`

## L99  (OK) — optimal 54, par 59

`T1->B1  T5->B1  T7->T5  T7->B1  T5->T7  T5->T7  T5->T1  T5->B1  T1->T5  T1->T5  T4->T1  T4->T5  T4->T5  T3->T1  T7->T4  T7->T4  T7->T4  T7->T3  T6->T7  T2->T6  T2->T7  T6->T2  T6->T2  T6->B1  T2->T6  T2->T6  T2->T6  T1->T2  T1->T2  T1->T2  T7->T1  T7->T1  T7->T1  T4->T7  T4->T7  T4->T7  T4->T7  T1->T4  T1->T4  T1->T4  T1->T4  T6->T1  T6->T1  T6->T1  T6->T1  T3->T6  T3->T6  T3->T6  T3->T7  T2->T3  T2->T3  T2->T3  T2->T3  T2->T6`

## L100  (OK) — optimal 54, par 59

`T4->B1  T6->B1  T6->B1  T2->T6  T3->T2  T5->T4  T5->T3  T5->B1  T1->T5  T1->T5  T2->T1  T2->T1  T3->T2  T3->T2  T3->T6  T3->T5  T2->T3  T2->T3  T2->T3  T4->T2  T4->T2  T7->T4  T7->T3  T4->T7  T4->T7  T4->B1  T4->T2  T5->T4  T5->T4  T5->T4  T5->T4  T6->T5  T6->T5  T6->T5  T1->T6  T1->T6  T1->T6  T7->T1  T7->T1  T7->T1  T7->T4  T2->T7  T2->T7  T2->T7  T2->T7  T1->T2  T1->T2  T1->T2  T1->T2  T6->T1  T6->T1  T6->T1  T6->T1  T6->T5`

## L101  (OK) — optimal 49, par 54

`T2->B1  T2->B1  T2->B2  T6->B2  T4->T6  T4->B2  T2->T4  T3->T2  T5->T2  T5->T4  T5->B1  T5->B2  T5->T2  T5->T3  T4->T5  T4->T5  T4->T5  T6->T5  T6->T5  T4->T6  T4->T5  T1->T4  T1->B1  T3->T4  T3->T4  T6->T4  T6->T4  T6->T1  T3->T6  T3->T1  T3->T2  T1->T3  T1->T3  T1->T3  T1->T3  T1->T3  B1->T1  B1->T1  B1->T1  B1->T1  T6->B1  T6->B1  T6->T1  B1->T6  B1->T6  B2->T6  B2->T6  B2->T6  B2->T6`

## L102  (OK) — optimal 50, par 55

`T1->B1  T5->B2  T6->B2  T6->B2  T4->T5  T4->B2  T4->B1  T4->T1  T4->B1  T5->T4  T5->T4  T5->T4  T2->T5  T2->T5  T2->T4  T2->T6  T2->B1  T1->T2  T1->T2  T3->T2  T6->T2  T6->T2  T6->T5  T6->T1  T5->T6  T5->T6  T5->T6  T5->T6  T3->T1  T3->T5  T3->T4  T3->T6  T1->T3  T1->T3  T1->T3  T1->T3  T1->T3  B2->T1  B2->T1  B2->T1  B2->T1  T5->B2  T5->B2  T5->T1  B1->T5  B1->T5  B1->T5  B1->T5  B2->T5  B2->T5`

## L103  (OK) — optimal 54, par 59

`T1->B1  T1->B2  T4->B2  T4->B1  T1->T4  B1->T1  B1->T1  T5->B1  T6->B1  T6->T1  T6->T4  T6->T5  T6->B1  T6->B2  T2->T6  T3->T6  B1->T6  B1->T6  B1->T6  T4->B1  T4->B1  T4->B1  T4->T2  T4->T6  T1->T4  T1->T4  T1->T4  T1->T4  T1->B2  T2->T1  T2->T1  T2->B1  T3->T1  T3->T1  T2->T3  T2->T1  T2->T4  T3->T2  T3->T2  T3->T2  T5->T2  T5->T2  T5->T2  B2->T3  B2->T3  B2->T3  B2->T3  T5->B2  T5->T3  B1->T5  B1->T5  B1->T5  B1->T5  B2->T5`

## L104  (OK) — optimal 54, par 59

`T1->B1  T1->B2  T1->B2  T5->B2  T5->B2  T6->T1  T6->B1  T6->B1  T2->T6  T2->T1  T2->T1  T2->B1  T5->T6  T2->T5  T3->T2  B1->T2  B1->T2  B1->T2  B1->T2  T5->B1  T5->B1  T1->T5  T1->T5  T1->T5  T1->T5  T1->T6  T1->B1  T6->T1  T6->T1  T6->T1  T6->T1  T6->T3  T6->B1  T3->T6  T3->T6  T4->T6  T4->T6  T4->T1  T4->T1  T4->T6  B1->T4  B1->T4  B1->T4  B1->T4  T3->B1  T3->T6  T3->B1  T3->T4  B1->T3  B1->T3  B2->T3  B2->T3  B2->T3  B2->T3`

## L105  (OK) — optimal 54, par 59

`T1->B1  T4->B1  T4->B1  T6->B2  T6->B1  T6->B2  T6->T4  T6->T1  T3->T6  T4->T6  T4->T6  T4->B2  T1->T4  T1->T4  T3->T1  T3->T6  T1->T3  T1->T3  T2->T1  T2->T4  T2->B2  T2->T1  T2->T3  T4->T2  T4->T2  T4->T2  T4->T2  T3->T4  T3->T4  T3->T4  T3->T4  T3->T1  T3->T4  T1->T3  T1->T3  T1->T3  T1->T3  T1->T6  T5->T3  T5->T3  B1->T1  B1->T1  B1->T1  B1->T1  T5->B1  T5->B1  T5->T1  T5->T2  B1->T5  B1->T5  B2->T5  B2->T5  B2->T5  B2->T5`

## L106  (OK) — optimal 50, par 55

`T2->B1  T2->B2  B1->T2  T1->B1  T5->B1  T5->B2  T5->B2  T5->B2  T5->B1  T4->T5  T4->B1  T6->T5  T6->T1  T6->T4  T1->T6  T1->T6  T1->T2  T1->T4  T1->T6  T1->T5  T6->T1  T6->T1  T6->T1  T6->T1  T6->T5  T6->T5  T4->T6  T4->T6  T4->T6  T4->T6  T2->T4  T2->T4  T2->T4  T2->T1  T2->T6  T2->T4  B1->T2  B1->T2  B1->T2  B1->T2  T3->B1  T3->T6  T3->T2  T3->T2  T3->T1  B1->T3  B2->T3  B2->T3  B2->T3  B2->T3`

## L107  (OK) — optimal 53, par 58

`T1->B1  T1->B2  T6->B1  T6->T1  T5->T6  T5->T6  T5->B2  T3->T1  T3->T5  T3->B2  T3->B1  T3->T5  T6->T3  T6->T3  T6->T3  T4->T5  T4->T3  T6->T4  T6->B2  T6->T4  T5->T6  T5->T6  T5->T6  T5->T6  T5->T6  B2->T5  B2->T5  B2->T5  B2->T5  T4->B2  T4->B2  T4->B2  T4->T6  T4->T5  T1->T4  T1->T4  T1->T4  T1->B2  T1->T3  T1->T4  T2->T1  T2->T1  B1->T1  B1->T1  B1->T1  T2->B1  T2->T1  T2->T4  B1->T2  B2->T2  B2->T2  B2->T2  B2->T2`

## L108  (OK) — optimal 51, par 56

`T1->B1  T2->B2  T2->B2  T5->B1  T3->T1  T3->T2  T3->B2  T3->B1  T3->B2  T6->T5  T3->T6  T1->T3  T1->T3  T1->T3  T4->T3  T6->T1  T6->T1  T6->T4  T5->T6  T5->T6  T5->T2  T5->T3  T5->T3  T2->T5  T2->T5  T2->T5  T2->T1  T2->B1  T6->T2  T6->T2  T6->T2  T6->T2  T6->T5  T4->T6  T4->T6  T4->T5  B2->T6  B2->T6  B2->T6  B2->T6  T4->B2  T1->T4  T1->T4  T1->T4  T1->T4  T1->T2  B1->T1  B1->T1  B1->T1  B1->T1  B2->T1`

## L109  (OK) — optimal 53, par 58

`T2->B1  T2->B2  T5->T2  T5->B1  T5->B2  T4->T2  T1->T4  T1->T5  T1->B1  T1->T5  T1->B2  T1->B1  T3->T1  T4->T1  T4->T1  T4->T1  T6->T1  T4->T3  T4->B2  T6->T5  T4->T6  T5->T4  T5->T4  T5->T4  T5->T4  T6->T5  T6->T5  T6->T4  T6->T4  B1->T6  B1->T6  B1->T6  B1->T6  T2->B1  T2->B1  T2->B1  T2->T6  T2->T1  T3->B1  T2->T5  B2->T2  B2->T2  B2->T2  B2->T2  T3->B2  T3->T2  T3->T5  T3->T2  B1->T3  B1->T3  B1->T3  B1->T3  B2->T3`

## L110  (OK) — optimal 45, par 50

`T1->B1  T1->B1  T4->B1  T5->B2  T4->T5  T4->B1  T4->B2  T4->B2  T1->T4  T3->T4  T3->B2  T3->T1  T3->T1  T6->T3  T2->T6  T2->T3  T2->T3  T2->T4  T5->T2  T5->T2  T5->T4  T1->T5  T1->T5  T1->T5  T1->T2  T1->T3  T5->T1  T5->T1  T5->T1  T5->T1  T6->T1  T6->T1  T5->T6  T5->T2  T6->T5  T6->T5  T6->T4  B1->T5  B1->T5  B1->T5  B1->T5  B2->T6  B2->T6  B2->T6  B2->T6`

## L111  (OK) — optimal 50, par 55

`T2->B1  T6->B2  T6->T2  T6->B1  T4->T6  T4->B1  T5->T4  T5->B2  T5->B1  T5->B2  T5->T6  T5->T4  B2->T5  B2->T5  B2->T5  T2->B2  T2->B2  T1->T2  T1->T5  T1->B2  T1->T2  T6->T1  T6->T1  T6->T1  T6->T5  T6->T1  T2->T6  T2->T6  T2->T6  T2->B2  T2->T6  T3->T2  T3->T5  T3->T6  T3->T2  T3->T6  T4->T2  T4->T2  T4->T2  B2->T3  B2->T3  B2->T3  B2->T3  T4->B2  T4->T3  B1->T4  B1->T4  B1->T4  B1->T4  B2->T4`

## L112  (OK) — optimal 51, par 56

`T2->B1  T2->B1  T2->B2  T6->B2  T6->T2  T6->T2  T6->B1  T4->T2  T6->T4  B1->T6  B1->T6  B1->T6  T3->B1  T4->B1  T4->B1  T4->T6  T1->T4  T1->B2  T1->T3  T1->B2  T1->B1  T1->T6  T3->T1  T3->T1  T3->T1  T4->T3  T4->T3  T4->T1  T4->T3  T2->T4  T2->T4  T2->T4  T2->T4  T2->T1  T3->T2  T3->T2  T3->T2  T3->T2  T3->T1  T5->T3  T5->T4  T5->T4  T5->T2  B1->T3  B1->T3  B1->T3  B1->T3  B2->T5  B2->T5  B2->T5  B2->T5`

## L113  (OK) — optimal 50, par 55

`T1->B1  T4->B2  T6->B1  T6->B2  T6->T1  T6->T4  T6->B2  T6->B1  T2->T6  T4->T6  T4->T6  T4->T6  T3->T4  T2->T3  T1->T2  T1->T2  T1->B2  T1->B1  T1->T4  T1->T6  T2->T1  T2->T1  T2->T1  T5->T1  T2->T5  T2->T6  T4->T2  T4->T2  T4->T2  T4->T1  B1->T4  B1->T4  B1->T4  B1->T4  T5->B1  T5->B1  T5->T4  T5->T2  T3->T5  T3->T5  T3->T5  T3->T2  T3->T1  T3->T5  B1->T3  B1->T3  B2->T3  B2->T3  B2->T3  B2->T3`

## L114  (OK) — optimal 49, par 54

`T1->B1  T2->B1  T3->B1  T3->B1  T4->B2  T1->T3  T6->T1  T6->B2  T2->T1  T5->T2  T6->T5  T6->T2  T6->B2  T5->T6  T5->T6  T5->B2  T3->T5  T3->T5  T3->T5  T3->T6  T1->T3  T1->T3  T1->T3  T1->T3  T1->T6  T5->T1  T5->T1  T5->T1  T5->T1  T5->T3  T5->T6  T2->T5  T2->T5  T2->T5  T2->T5  T2->T5  T4->T5  B1->T2  B1->T2  B1->T2  B1->T2  T4->B1  T4->T1  T4->T2  B1->T4  B2->T4  B2->T4  B2->T4  B2->T4`

## L115  (OK) — optimal 51, par 56

`T2->B1  T4->B2  T6->B1  T3->T2  T4->T3  T4->B2  T4->B1  T4->T6  T4->B2  T2->T4  T2->T4  T5->T4  T5->T4  T5->T2  T5->T4  T3->T5  T3->T5  T3->B1  T3->B2  T2->T5  T2->T5  T2->T3  T2->T3  T2->T4  T5->T2  T5->T2  T5->T2  T5->T2  T5->T2  T3->T5  T3->T5  T3->T5  B1->T3  B1->T3  B1->T3  B1->T3  T1->B1  T1->T5  T1->T5  T1->B1  T1->T2  T6->T1  T6->T1  T6->T1  T6->T3  B1->T1  B1->T1  B2->T6  B2->T6  B2->T6  B2->T6`

## L116  (OK) — optimal 47, par 52

`T1->B1  T4->B2  T3->T1  T4->T3  T2->T4  T2->B1  T2->B2  T6->T4  T6->B2  T2->T6  T2->B1  T2->B1  T4->T2  T4->T2  T4->T2  T4->T2  T3->T4  T3->T4  T3->T4  T6->T3  T6->T3  T5->T6  T5->T2  T5->T3  T5->T2  T5->T4  T5->B2  T3->T5  T3->T5  T3->T5  T3->T5  T3->T5  T1->T3  T1->T3  T6->T3  T6->T3  T6->T5  T1->T6  T1->T3  B1->T6  B1->T6  B1->T6  B1->T6  B2->T1  B2->T1  B2->T1  B2->T1`

## L117  (OK) — optimal 48, par 53

`T1->B1  T3->B2  T5->T3  T5->B2  T1->T5  T1->B2  T6->T5  T6->B1  T2->T6  T2->T6  T2->T1  T2->B1  T2->T1  T3->T2  T3->T2  T3->B2  T3->T2  T3->T2  T3->B1  B2->T3  B2->T3  B2->T3  B2->T3  T5->B2  T5->B2  T5->B2  T5->T3  T5->T3  T6->T5  T6->T5  T6->T5  T6->T2  T6->T5  B2->T6  B2->T6  B2->T6  T4->B2  T4->T5  T4->T6  T1->T4  T1->T4  T1->T4  T1->T6  B1->T1  B1->T1  B1->T1  B1->T1  B2->T1`

## L118  (OK) — optimal 54, par 59

`T2->B1  T2->B2  T2->B1  T3->B1  T2->T3  T2->B2  T2->B1  T4->T2  T4->T2  B2->T2  B2->T2  T4->B2  T4->T2  T4->T2  T5->B2  T4->T5  B2->T4  B2->T4  T3->B2  T3->B2  T6->T3  T6->T4  T6->T4  T6->T4  T6->B2  T6->T4  T5->T6  T5->T6  T5->T6  T5->B2  T5->T6  T5->T3  B2->T5  B2->T5  B2->T5  B2->T5  T3->B2  T3->B2  T3->B2  T3->T5  T3->T6  B2->T3  B2->T3  B2->T3  T1->B2  T1->T3  T1->T5  T1->T3  T1->T6  B1->T1  B1->T1  B1->T1  B1->T1  B2->T1`

## L119  (OK) — optimal 53, par 58

`T1->B1  T6->T1  T6->B1  T2->T6  T2->B2  T2->B2  T2->B2  T2->B1  T2->T6  T1->T2  T1->T2  T1->B2  T1->T2  T1->T2  T4->T1  T4->T2  T1->T4  T1->T4  T3->T1  B1->T1  B1->T1  B1->T1  T6->B1  T6->B1  T6->B1  T6->T1  T4->T6  T4->T6  T4->T6  T4->T2  T3->T4  T3->T4  T3->B1  T3->T6  T4->T3  T4->T3  T4->T3  T5->T3  B1->T4  B1->T4  B1->T4  B1->T4  T5->B1  T5->T1  T5->B1  T5->T3  T5->T4  B1->T5  B1->T5  B2->T5  B2->T5  B2->T5  B2->T5`

## L120  (OK) — optimal 46, par 51

`T5->B1  T6->B1  T3->T6  T3->B2  T3->B1  T6->T3  T6->T3  T1->T6  T1->T6  T1->T3  T2->T1  T2->T1  T2->B2  T2->T5  T2->B1  T5->T2  T5->T2  T5->T1  T5->T2  T5->T2  T1->T5  T1->T5  T1->T5  T1->T5  T1->B2  T6->T1  T6->T1  T6->T1  T6->T5  T6->B2  T4->T6  T4->T1  B1->T6  B1->T6  B1->T6  B1->T6  T4->B1  T4->T2  T4->B1  T4->T1  B1->T4  B1->T4  B2->T4  B2->T4  B2->T4  B2->T4`

## L121  (OK) — optimal 49, par 54

`T3->B1  T3->B1  T3->B2  T2->T3  T1->T2  T1->T3  T1->B2  T1->B1  T1->B2  T3->T1  T3->T1  T3->T1  T3->B1  T3->T1  T2->T3  T2->T3  T4->T3  T5->T3  T6->T3  T6->B2  T2->T6  T2->T1  T2->T4  T5->T2  B1->T2  B1->T2  B1->T2  B1->T2  T5->B1  T6->B1  T6->B1  T6->B1  T5->T6  T5->T3  T5->T6  T4->T5  T4->T5  T4->T6  B2->T5  B2->T5  B2->T5  B2->T5  T4->B2  T4->T6  B1->T4  B1->T4  B1->T4  B1->T4  B2->T4`

## L122  (OK) — optimal 48, par 53

`T1->B1  T1->B2  T4->T1  T2->T4  T2->B1  T2->B2  T2->B1  T2->B2  T4->T2  T4->T2  T4->T2  T4->B2  T4->B1  T4->T1  B1->T4  B1->T4  B1->T4  B1->T4  T3->B1  T3->T4  T3->T2  T5->B1  T5->B1  T5->T3  T5->B1  T5->T2  T1->T5  T1->T5  T1->T5  T1->T5  T1->T4  T3->T1  T3->T1  T3->T1  T6->T3  B2->T3  B2->T3  B2->T3  B2->T3  T6->B2  T6->T1  T6->T1  T6->T5  B1->T6  B1->T6  B1->T6  B1->T6  B2->T6`

## L123  (OK) — optimal 50, par 55

`T2->B1  T3->B2  T6->B1  T1->T2  T1->B1  T4->T3  T4->T6  T4->T1  T6->T4  T6->T4  T5->T6  T5->T6  T5->B2  T5->B2  T5->B1  T6->T5  T6->T5  T6->T5  T6->T1  T6->T4  T6->T5  T1->T6  T1->T6  T1->T6  T1->T5  T1->T6  T2->T6  T2->T6  T2->T1  T2->B2  T2->T1  T3->T1  T3->T1  T3->T2  T3->T1  B1->T2  B1->T2  B1->T2  B1->T2  T3->B1  T4->T3  T4->T3  T4->T3  T4->T3  T4->T3  B1->T4  B2->T4  B2->T4  B2->T4  B2->T4`

## L124  (OK) — optimal 53, par 58

`T2->B1  T2->B2  T2->B1  T2->B1  T2->B2  T4->B2  T5->T4  T5->B2  T5->B1  T2->T5  B1->T2  B1->T2  B1->T2  B1->T2  T1->B1  T1->B1  T1->T5  T1->T2  T3->T5  T3->B1  T1->T3  T6->T1  T6->T1  T3->T6  T3->T6  T3->B1  T3->T1  T5->T3  T5->T3  T5->T3  T5->T3  T5->T2  T4->T5  T4->T5  T6->T5  T6->T5  T6->T5  T6->T1  T6->T3  T4->T6  B2->T6  B2->T6  B2->T6  B2->T6  T4->B2  T4->B2  T4->T1  B1->T4  B1->T4  B1->T4  B1->T4  B2->T4  B2->T4`

## L125  (OK) — optimal 48, par 53

`T1->B1  T1->B1  T4->B2  T4->B2  T3->T4  T2->T3  T1->T2  T1->B2  T5->T1  T5->B1  T2->T5  T2->T5  T2->B1  T2->T1  T2->T1  T5->T2  T5->T2  T5->T2  T4->T5  T4->T5  T4->T2  T3->T4  T3->T4  T6->T5  T3->T6  T3->T1  T3->B2  T5->T3  T5->T3  T5->T3  T5->T3  T5->T3  T4->T5  T4->T5  T4->T5  T4->T2  T6->T4  T6->T4  T6->T5  T6->T5  B1->T4  B1->T4  B1->T4  B1->T4  B2->T6  B2->T6  B2->T6  B2->T6`

## L126  (OK) — optimal 52, par 57

`T2->B1  T3->B2  T3->B1  T6->T2  T6->B1  T1->T6  T1->T3  T4->T1  T4->T3  T4->T6  T4->B1  T4->B2  T6->T4  T6->T4  T6->T4  T6->T4  T6->T1  T6->T4  B1->T6  B1->T6  B1->T6  B1->T6  T2->B1  T2->B1  T2->B1  T2->T6  T2->B2  T3->T2  T3->T2  T3->T2  T3->T2  T3->T6  T3->B1  T1->T3  T1->T3  T1->T3  T1->B2  T1->T3  T5->T3  T5->T2  B2->T1  B2->T1  B2->T1  B2->T1  T5->B2  T5->T3  T5->T1  B1->T5  B1->T5  B1->T5  B1->T5  B2->T5`

## L127  (OK) — optimal 52, par 57

`T2->B1  T4->B1  T4->B2  T6->B2  T2->T6  T2->B2  T2->B2  T6->T2  T6->T2  T6->B1  T6->T4  T6->T4  T3->T6  B1->T6  B1->T6  B1->T6  T3->B1  T3->B1  T3->T6  T5->B1  T3->T5  T1->T3  T4->T3  T4->T3  T4->T3  T4->T2  T4->T1  T4->T3  T1->T4  T1->T4  T1->B1  T5->T4  T5->T4  T5->T4  T1->T2  T1->T4  T5->T1  B2->T1  B2->T1  B2->T1  B2->T1  T5->B2  T2->T5  T2->T5  T2->T5  T2->T5  T2->T5  B1->T2  B1->T2  B1->T2  B1->T2  B2->T2`

## L128  (OK) — optimal 48, par 53

`T2->B1  T3->B1  T3->B2  T5->B1  T5->T2  T5->B2  T5->B2  T5->B2  T5->T3  T1->T5  T2->T5  T2->T5  T4->T5  T4->T5  T4->B1  T6->T1  T4->T6  T4->T3  T4->T2  T1->T4  T1->T4  T1->T4  T1->T4  T3->T1  T3->T1  T3->T1  T3->T5  T3->T2  B2->T3  B2->T3  B2->T3  B2->T3  T6->B2  T6->B2  T6->T4  T6->T3  T2->T6  T2->T6  T2->T6  T2->T4  T2->T1  T2->T6  B1->T2  B1->T2  B1->T2  B1->T2  B2->T2  B2->T2`

## L129  (OK) — optimal 50, par 55

`T3->B1  T4->B2  T5->B1  T1->T3  T1->T4  T1->B2  T1->B1  T1->B1  T1->B2  T2->T1  T2->B2  T2->T5  T2->T1  T2->T1  T4->T2  T4->T2  T4->T1  T4->T2  T4->T1  T4->T2  T6->T4  B1->T4  B1->T4  B1->T4  B1->T4  T5->B1  T5->B1  T5->B1  T5->T6  T5->B1  T3->T5  T3->T5  T3->T5  T3->T5  T3->T4  T3->T2  T6->T3  T6->T3  T6->T5  B2->T3  B2->T3  B2->T3  B2->T3  T6->B2  T6->T1  B1->T6  B1->T6  B1->T6  B1->T6  B2->T6`

## L130  (OK) — optimal 46, par 51

`T2->B1  T2->B1  T4->B1  T6->B1  T6->B2  T6->B2  T2->T6  T2->T4  T2->B2  T1->T2  T1->T2  T5->T6  T5->T2  T5->T2  T4->T1  T4->T1  T5->T4  T5->T6  T3->T5  T4->T5  T4->T5  T4->T5  T4->B2  T6->T4  T6->T4  T6->T4  T6->T4  T6->T3  T6->T5  T1->T6  T1->T6  T1->T6  T1->T6  T1->T4  T3->T6  T3->T6  T3->T2  T3->T1  B1->T1  B1->T1  B1->T1  B1->T1  B2->T3  B2->T3  B2->T3  B2->T3`

## L131  (OK) — optimal 51, par 56

`T4->B1  T6->T4  T2->T6  T2->B2  T2->B2  T2->B1  T2->B2  T2->B1  T3->T2  T3->T2  T4->T2  T4->T2  T3->T4  T3->T2  T3->B2  T3->T4  T1->T3  T5->T3  T5->B1  T6->T3  T6->T3  T6->T3  T1->T5  T6->T5  T6->T1  B1->T6  B1->T6  B1->T6  B1->T6  T5->B1  T5->B1  T5->B1  T5->T2  T5->T6  T4->T5  T4->T5  T4->T5  T4->T3  T4->T5  T4->B1  T1->T4  T1->T4  T1->T5  B1->T1  B1->T1  B1->T1  B1->T1  B2->T4  B2->T4  B2->T4  B2->T4`

## L132  (OK) — optimal 53, par 58

`T2->B1  T4->B2  T5->B1  T6->B2  T1->T2  T4->T6  T4->T1  T2->T4  T2->T4  T2->T4  T2->B2  T2->B1  T2->B2  T1->T2  T1->T2  T5->T1  T5->B1  T5->T2  T5->T2  T6->T5  T6->T5  T1->T6  T1->T6  T1->T2  T1->T5  T3->T1  T3->T2  B2->T1  B2->T1  B2->T1  B2->T1  T3->B2  T6->B2  T6->B2  T6->B2  T3->T6  T3->T5  T4->T3  T4->T3  T4->T3  T4->T3  T4->T3  T4->T5  T6->T4  T6->T4  B1->T4  B1->T4  B1->T4  B1->T4  B2->T6  B2->T6  B2->T6  B2->T6`

## L133  (OK) — optimal 52, par 57

`T3->B1  T3->B1  T4->B2  T5->B2  T5->B2  T1->T4  T1->B2  T2->T1  T5->T1  T6->T2  T5->T6  T5->B1  T5->T3  B2->T5  B2->T5  B2->T5  B2->T5  T6->B2  T6->B2  T6->B2  T6->B2  T6->B1  T1->T6  T1->T6  T1->T6  T3->T1  T3->T1  T3->T6  T3->T5  B1->T3  B1->T3  B1->T3  B1->T3  T4->B1  T4->B1  T4->T3  T4->T5  T4->T1  T2->T4  T2->T4  B1->T4  B1->T4  T2->B1  T2->B1  T2->T6  T2->T4  B1->T2  B1->T2  B2->T2  B2->T2  B2->T2  B2->T2`

## L134  (OK) — optimal 54, par 59

`T1->B1  T4->B2  T3->T1  T3->B2  T3->T4  T1->T3  T1->T3  T1->B1  T1->T3  T4->T1  T4->T1  T4->B1  T5->T1  T6->T4  T5->T6  T5->T4  T5->B1  T5->B2  T2->T5  T6->T5  T6->T5  T6->T1  T6->B2  T6->T5  T6->T4  T3->T6  T3->T6  T3->T6  T3->T6  T3->T6  T3->T5  T2->T3  B1->T3  B1->T3  B1->T3  B1->T3  T2->B1  T2->T6  T2->T3  T4->T2  T4->T2  T4->T2  T4->T2  T4->T2  T1->T4  T1->T4  T1->T4  T1->T4  T1->T4  B1->T1  B2->T1  B2->T1  B2->T1  B2->T1`

## L135  (OK) — optimal 51, par 56

`T2->B1  T5->B1  T5->B1  T5->B1  T1->T5  T1->B2  T4->T5  T4->T1  T4->B2  T3->T2  T4->T5  T4->T3  T4->B2  T2->T4  T2->T4  T6->T4  T2->T1  T2->T4  T2->B2  T2->T6  T5->T2  T5->T2  T5->T2  T5->T2  T5->T4  B2->T5  B2->T5  B2->T5  B2->T5  T3->B2  T3->B2  T3->T2  T3->T4  T3->B2  T1->T3  T1->T3  T1->T3  T1->B2  T1->T5  T6->T3  T6->T3  T6->T2  T6->T1  B1->T1  B1->T1  B1->T1  B1->T1  B2->T6  B2->T6  B2->T6  B2->T6`

## L136  (OK) — optimal 51, par 56

`T3->B1  T3->B1  T4->B2  T4->B2  T2->T3  T2->B1  T2->B2  T1->T3  T1->T2  T1->B2  T1->B1  T4->T2  T1->T4  T2->T1  T2->T1  T2->T1  T2->T4  T5->T2  T5->T2  T5->T1  T6->T4  T5->T6  T5->T2  T4->T5  T4->T5  T4->T5  T4->T5  T4->T2  B2->T4  B2->T4  B2->T4  B2->T4  T6->B2  T6->B2  T6->B2  T6->T5  T6->T4  T6->T1  T3->T6  T3->T6  T3->T6  B2->T6  B2->T6  B2->T6  T3->B2  T3->T2  B1->T3  B1->T3  B1->T3  B1->T3  B2->T3`

## L137  (OK) — optimal 51, par 56

`T1->B1  T1->B2  T4->B2  T5->T4  T5->B1  T6->T1  T5->T6  T5->B1  T5->T1  T5->B1  T1->T5  T1->T5  T1->T5  T1->B2  T1->B2  T2->T5  T1->T2  T3->T1  T3->T1  T3->T1  T4->T1  T4->T1  T4->T5  T2->T4  T2->T4  T3->T2  T3->T4  T3->T1  T2->T3  T2->T3  T2->T3  T2->T5  T6->T3  T6->T3  T6->T3  T6->T2  B1->T2  B1->T2  B1->T2  B1->T2  T6->B1  T4->T6  T4->T6  T4->T6  T4->T6  T4->T6  B1->T4  B2->T4  B2->T4  B2->T4  B2->T4`

## L138  (OK) — optimal 55, par 60

`T1->B1  T2->B1  T5->B2  T5->B2  T6->B2  T6->T5  T6->B1  T1->T6  T3->T1  T3->B1  T2->T6  T5->T2  T5->T2  T5->T3  T5->T6  T5->T1  T2->T5  T2->T5  T2->T5  T2->T5  T2->T3  T2->T5  T6->T2  T6->T2  T6->T2  T6->T2  T6->T5  T6->B2  T1->T6  T1->T6  T1->T6  T1->T2  T1->T2  T3->T1  T3->T1  T3->T1  T3->T6  T3->T6  T3->T1  T4->T3  B1->T3  B1->T3  B1->T3  B1->T3  T4->B1  T4->T6  T4->T3  T4->B1  T4->T1  B1->T4  B1->T4  B2->T4  B2->T4  B2->T4  B2->T4`

## L139  (OK) — optimal 51, par 56

`T3->B1  T3->B2  T3->B2  T3->B2  T3->B1  T4->B2  T5->T3  T5->T4  T3->T5  T3->T5  T2->T3  T2->T3  T2->T3  T6->T3  T2->T6  T2->B1  T6->T2  T6->T2  T4->T6  T4->T6  T4->T2  T4->T3  T4->T2  T4->B1  T1->T4  T5->T4  T5->T4  T5->T4  T5->T4  T5->T4  T5->T2  T1->T5  B2->T5  B2->T5  B2->T5  B2->T5  T1->B2  T6->B2  T6->B2  T6->B2  T6->T3  T6->T5  T1->T6  B1->T6  B1->T6  B1->T6  B1->T6  B2->T1  B2->T1  B2->T1  B2->T1`

## L140  (OK) — optimal 44, par 49

`T2->B1  T5->B1  T6->B2  T6->B2  T6->B1  T6->B2  T4->T5  T4->B2  T6->T2  T4->T6  T5->T4  T5->T4  T5->T6  T5->T4  T5->T6  T5->B1  T3->T5  T3->T6  T4->T5  T4->T5  T4->T5  T4->T5  T1->T4  T3->T4  T3->T4  T3->T6  B2->T3  B2->T3  B2->T3  B2->T3  T1->B2  T2->B2  T2->B2  T2->T4  T2->T3  T2->T5  T1->T2  B1->T2  B1->T2  B1->T2  B1->T2  B2->T1  B2->T1  B2->T1`

## L141  (OK) — optimal 49, par 54

`T5->B1  T5->B2  T5->B2  T5->B1  T6->T5  T6->B2  T6->T5  B1->T6  B1->T6  T1->B1  T2->B1  T2->B2  T4->T6  T4->T5  T4->T1  T4->T5  T4->T2  T4->B1  T6->T4  T6->T4  T6->T4  T6->T4  T6->T2  T1->T6  T1->T6  T1->T6  T1->T6  T1->B1  T2->T1  T2->T1  T2->T1  T2->T1  T2->T4  B2->T2  B2->T2  B2->T2  B2->T2  T3->B2  T3->T1  T3->T6  T3->T4  T3->B2  T3->T2  B1->T3  B1->T3  B1->T3  B1->T3  B2->T3  B2->T3`

## L142  (OK) — optimal 51, par 56

`T4->B1  T4->B2  T4->B1  T4->B1  T4->B2  T5->B2  T6->T5  T6->T4  T6->B1  T3->T6  T3->T4  T1->T6  T1->T3  T1->B2  T1->T6  T1->T3  T5->T1  T5->T1  T5->T1  T5->T4  T3->T5  T3->T5  T3->T5  T3->T4  T3->T1  T3->T4  T2->T3  T2->T3  T5->T3  T5->T3  T5->T3  T5->T3  T6->T5  T6->T5  T6->T5  T6->T5  T6->T1  B1->T6  B1->T6  B1->T6  B1->T6  T2->B1  T2->T6  T2->B1  T2->T5  B1->T2  B1->T2  B2->T2  B2->T2  B2->T2  B2->T2`

## L143  (OK) — optimal 55, par 60

`T4->B1  T5->B2  T6->B2  T2->T4  T2->B2  T3->T2  T3->T5  T3->B1  T3->B1  T3->T2  T4->T3  T4->T3  T4->T3  T4->T6  T4->B1  T4->T3  T5->T4  T5->T4  T5->T4  T2->T5  T2->T5  T2->T5  T2->T3  T2->T4  T2->B2  T1->T2  T5->T2  T5->T2  T5->T2  T5->T2  T5->T4  B2->T5  B2->T5  B2->T5  B2->T5  T6->B2  T6->B2  T6->B2  T6->B2  T6->T2  B1->T6  B1->T6  B1->T6  B1->T6  T1->B1  T1->T4  T1->B1  T1->T6  T1->T5  B1->T1  B1->T1  B2->T1  B2->T1  B2->T1  B2->T1`

## L144  (OK) — optimal 54, par 59

`T1->B1  T1->B2  T1->B1  T3->B1  T3->B2  T4->B2  T5->B1  T2->T1  T3->T5  T2->T3  T2->T4  T2->T1  T2->B2  B1->T2  B1->T2  B1->T2  B1->T2  T4->B1  T4->B1  T4->B1  T4->T3  T4->T1  T5->T4  T5->T4  T5->T2  T5->T4  T5->T3  T5->B1  T3->T5  T3->T5  T3->T5  T3->T5  T3->T4  B1->T3  B1->T3  B1->T3  B1->T3  T6->B1  T6->T4  T6->B1  T6->T5  T6->T5  T1->T6  T1->T6  T1->T6  T1->T6  T1->T3  T1->T6  B1->T1  B1->T1  B2->T1  B2->T1  B2->T1  B2->T1`

## L145  (OK) — optimal 52, par 57

`T5->B1  T6->B2  T6->B1  T6->B2  T1->T5  T1->B1  T2->T6  T2->B2  T2->T1  T2->B2  T2->T6  T1->T2  T1->T2  T3->T2  T3->T1  T3->T2  T5->T1  T5->T1  T5->T3  T5->B1  T5->T6  T5->T3  T6->T5  T6->T5  T6->T5  T6->T5  T6->T2  T4->T3  T4->T6  B2->T6  B2->T6  B2->T6  B2->T6  T4->B2  T4->T5  T4->T5  T3->T4  T3->T4  T3->T4  T3->T4  T3->B2  T3->T4  T1->T3  T1->T3  T1->T3  T1->T3  B1->T1  B1->T1  B1->T1  B1->T1  B2->T3  B2->T3`

## L146  (OK) — optimal 53, par 58

`T1->B1  T2->B1  T2->B2  T3->B2  T5->B1  T6->B2  T2->T1  T3->T2  T4->T5  T4->T3  T6->T4  T6->T2  T6->B2  T6->B1  T6->T4  B2->T6  B2->T6  B2->T6  B2->T6  T4->B2  T4->B2  T4->B2  T4->T3  T4->T6  T1->T4  T1->T4  T1->B2  T5->T4  T5->T4  T5->T4  T1->T2  T1->T5  T3->T1  T3->T1  T3->T1  T3->T6  T3->T1  T2->T3  T2->T3  T2->T3  T2->T3  T2->T3  T2->T1  T5->T2  T5->T2  B1->T2  B1->T2  B1->T2  B1->T2  B2->T5  B2->T5  B2->T5  B2->T5`

## L147  (OK) — optimal 51, par 56

`T1->B1  T1->B2  T3->B2  T5->B1  T1->T3  T1->T5  T1->B2  T3->T1  T3->T1  T4->T1  T4->B1  T4->T1  T4->B1  T4->T3  T4->T3  T5->T4  T5->T4  T5->T4  T6->T5  T6->T4  T6->B2  T2->T5  T6->T2  T6->T5  T2->T6  T2->T6  T3->T6  T3->T6  T3->T6  T2->T3  T2->T1  T2->T4  T5->T2  T5->T2  T5->T2  T5->T2  T5->T2  B2->T5  B2->T5  B2->T5  B2->T5  T3->B2  T3->B2  T3->T4  T3->T5  B1->T3  B1->T3  B1->T3  B1->T3  B2->T3  B2->T3`

## L148  (OK) — optimal 53, par 58

`T1->B1  T1->B1  T2->B1  T3->B2  T3->B1  T2->T3  T1->T2  T1->T3  T5->T2  B2->T5  T1->B2  T4->T1  T5->T1  T5->T1  T4->T5  T4->T1  T2->T4  T2->T4  T2->T4  T2->T5  T2->B2  T4->T2  T4->T2  T4->T2  T4->T2  T4->B2  T5->T4  T5->T4  T5->T4  T6->T2  T5->T6  T5->B2  T5->T4  T3->T5  T3->T5  T3->T5  T3->T5  T3->T4  T3->T5  T6->T3  T6->T3  B1->T3  B1->T3  B1->T3  B1->T3  T6->B1  T6->T5  T6->T1  B1->T6  B2->T6  B2->T6  B2->T6  B2->T6`

## L149  (OK) — optimal 49, par 54

`T4->B1  T4->B2  T4->B1  T6->B1  T2->T4  T3->T4  T3->T6  B2->T2  T3->B2  T3->B2  T3->B1  T3->T4  T6->T3  T6->T3  T6->T3  T6->T3  T6->B2  T1->T6  T1->T3  T2->T6  T2->T6  T2->T1  T2->T6  T2->T3  T4->T2  T4->T2  T4->T2  T4->T2  T4->B2  T5->T4  T5->T4  T5->T4  T5->T6  T5->T2  B2->T5  B2->T5  B2->T5  B2->T5  T1->B2  T1->B2  T1->T4  T1->T4  T1->T5  B1->T1  B1->T1  B1->T1  B1->T1  B2->T1  B2->T1`

## L150  (OK) — optimal 48, par 53

`T2->B1  T2->B2  T2->B2  T2->B1  T2->B2  T3->B2  T4->T2  T4->B1  T6->T3  T6->T2  T6->T2  T6->T2  T3->T6  T3->T6  T3->B1  T5->T4  T5->T6  T5->T6  T5->T3  T5->T2  T3->T5  T3->T5  T4->T3  T4->T3  T4->T5  T3->T4  T3->T4  T3->T4  T3->T5  B1->T3  B1->T3  B1->T3  B1->T3  T1->B1  T1->T3  T1->T5  T1->T3  T1->T4  T6->T1  T6->T1  T6->T1  T6->T1  T6->T1  B1->T6  B2->T6  B2->T6  B2->T6  B2->T6`

## L151  (OK) — optimal 62, par 67

`T4->B1  T5->B2  T5->B1  T5->T4  T1->T5  T1->B2  T2->T5  T2->B2  T1->T5  T1->T2  T4->T1  T4->T1  T4->B1  T3->T1  T4->T3  T4->T1  T6->T2  T4->T6  T1->T4  T1->T4  T1->T4  T1->T4  T1->T4  T3->T1  T3->T1  B1->T1  B1->T1  B1->T1  T5->B1  T6->B1  T6->B1  T6->T3  B2->T6  B2->T6  B2->T6  T5->B2  T5->B2  T5->B2  T6->T5  T6->T5  T6->T5  T6->T5  T6->T4  T3->T6  T3->T6  T2->T3  T2->T3  T2->T3  T2->T6  T2->T6  T5->T2  T5->T2  T5->T2  T5->T2  T5->T2  T5->T6  B1->T5  B1->T5  B1->T5  B2->T5  B2->T5  B2->T5`

## L152  (OK) — optimal 59, par 64

`T1->B1  T3->B1  T5->B2  T5->B2  T6->B1  T4->T1  T5->T6  T3->T5  T4->T5  T4->T3  T4->T5  T4->B2  T4->T3  T2->T4  T5->T4  T5->T4  T5->T4  T5->T4  T5->T2  T1->T5  T1->T5  T2->T1  T2->T1  T2->T4  T6->T2  T6->T2  T6->T5  T6->T5  T3->T6  T3->T6  T3->T6  T2->T3  T2->T3  T2->T3  T2->T5  B2->T2  B2->T2  B2->T2  T1->B2  T1->B2  T1->B2  T1->T6  T3->T1  T3->T1  T3->T1  T3->T1  T3->T2  T1->T3  T1->T3  T1->T3  T1->T3  T1->T3  T1->T2  B1->T1  B1->T1  B1->T1  B2->T1  B2->T1  B2->T1`

## L153  (OK) — optimal 60, par 65

`T1->B1  T1->B1  T3->B2  T2->T1  T2->B2  T4->T3  T6->T4  T6->T1  T5->T6  T2->T5  T2->T6  T2->B1  T3->T2  T3->T2  T6->T2  T6->T2  T6->T2  T6->B2  T3->T6  B1->T6  B1->T6  B1->T6  T3->B1  T3->B1  T1->T3  T1->T3  T1->T3  T1->B1  T1->T3  T6->T1  T6->T1  T6->T1  T6->T1  T6->T1  T4->T6  T4->T6  T5->T6  T5->T6  T4->T5  T4->T5  B2->T4  B2->T4  B2->T4  T5->B2  T5->B2  T5->B2  T5->T4  T5->T6  T4->T5  T4->T5  T4->T5  T4->T5  T4->T5  T4->T3  B1->T4  B1->T4  B1->T4  B2->T4  B2->T4  B2->T4`

## L154  (OK) — optimal 59, par 64

`T1->B1  T1->B2  T5->B2  T2->T5  T2->B2  T6->T1  T6->B1  T3->T2  T3->T2  T3->T6  T3->T1  T2->T3  T2->T3  T2->T3  T2->B1  T2->T6  B1->T2  B1->T2  B1->T2  T5->B1  T5->B1  T5->T2  T6->T5  T6->T5  T6->T5  T6->B1  T6->T2  T1->T6  T1->T6  T1->T6  T4->T6  T1->T4  T1->T6  T3->T1  T3->T1  T3->T1  T3->T1  B1->T3  B1->T3  B1->T3  T4->B1  T4->B1  T4->T1  T4->T3  T5->B1  B2->T4  B2->T4  B2->T4  T5->B2  T5->B2  T5->B2  T5->T4  T5->T3  B1->T5  B1->T5  B1->T5  B2->T5  B2->T5  B2->T5`

## L155  (OK) — optimal 62, par 67

`T4->B1  T4->B1  T4->B2  T5->B2  T5->B1  T1->T4  T6->T4  T6->T5  T2->T6  T2->T1  T3->T6  T2->T3  T5->T2  T5->T2  T5->T4  T5->B2  T5->T2  B2->T5  B2->T5  B2->T5  T3->B2  T3->B2  T3->T5  T3->B2  T2->T3  T2->T3  T2->T3  T2->T3  B1->T2  B1->T2  B1->T2  T1->B1  T1->B1  T1->T2  T1->B1  T1->T5  T3->T1  T3->T1  T3->T1  T3->T1  T3->T1  T4->T3  T4->T3  T4->T3  T4->T3  T6->T4  T6->T4  T6->T4  T6->T5  T6->T4  T2->T6  T2->T6  T2->T6  T2->T6  T2->T6  T2->T3  B1->T2  B1->T2  B1->T2  B2->T2  B2->T2  B2->T2`

## L156  (OK) — optimal 58, par 63

`T6->B1  T6->B2  B1->T6  T1->B1  T4->T1  T4->B2  T2->T6  T2->T4  T3->T4  T3->T2  T3->B2  T3->B1  B2->T3  B2->T3  B2->T3  T2->B2  T2->B2  T2->B1  T2->B2  T2->T3  T6->T2  T6->T2  T6->T2  T6->T2  T4->T6  T4->T6  T4->T6  T4->T2  T6->T4  T6->T4  T6->T4  T6->T4  T6->T2  T4->T6  T4->T6  T4->T6  T4->T6  T4->T6  T5->T6  B1->T4  B1->T4  B1->T4  T5->B1  T5->B1  T1->T5  T1->T5  T1->B1  T5->T1  T5->T1  T5->T1  T5->T4  T5->T4  B1->T5  B1->T5  B1->T5  B2->T5  B2->T5  B2->T5`

## L157  (OK) — optimal 58, par 63

`T3->B1  T3->B1  T4->B1  T1->T3  T1->T4  T1->T3  T1->B2  T4->T1  T4->T1  T4->B2  T5->T4  T5->B2  T5->T4  T5->T1  T6->T5  B2->T5  B2->T5  B2->T5  T6->B2  T6->B2  T6->T4  T6->B2  T6->T1  T5->T6  T5->T6  T5->T6  T5->T6  T5->T6  T2->T5  B2->T5  B2->T5  B2->T5  T3->B2  T3->B2  T3->B2  T3->T2  T3->T5  T2->T3  T2->T3  B1->T3  B1->T3  B1->T3  T4->B1  T4->B1  T4->B1  T4->T2  B2->T4  B2->T4  B2->T4  T2->B2  T2->B2  T2->T4  T2->T6  B1->T2  B1->T2  B1->T2  B2->T2  B2->T2`

## L158  (OK) — optimal 67, par 72

`T1->B1  T1->B2  T4->B1  T5->B2  T2->T1  T4->T1  T4->T2  T5->T4  T5->B2  T6->T5  T6->B1  T6->T4  T6->T4  T6->T5  B2->T6  B2->T6  B2->T6  T2->B2  T3->B2  T3->B2  T3->T6  T3->T5  T4->T3  T4->T3  T4->T3  T4->T3  B1->T4  B1->T4  B1->T4  T2->B1  T2->B1  T1->T2  T1->T2  T1->T2  T1->T4  T1->B1  T5->T1  T5->T1  T5->T1  T5->T1  T2->T5  T2->T5  T2->T5  T2->T5  T2->T1  T5->T2  T5->T2  T5->T2  T5->T2  T5->T2  T3->T5  T3->T5  T3->T5  T3->T5  T3->T5  T4->T3  T4->T3  T4->T3  T4->T3  T4->T3  T4->T6  B1->T4  B1->T4  B1->T4  B2->T4  B2->T4  B2->T4`

## L159  (OK) — optimal 64, par 69

`T3->B1  T1->T3  T5->T1  T5->B1  T5->B1  T5->B2  T2->T5  T2->T5  T1->T2  T1->T2  T4->T5  T6->T1  T4->T6  T3->T4  T3->T4  T3->T5  T2->T3  T2->T3  T2->T3  B2->T2  T6->B2  T6->B2  T1->T6  T1->T6  T1->T2  T1->B2  T3->T1  T3->T1  T3->T1  T3->T1  T2->T3  T2->T3  T2->T3  T4->T2  T4->T2  T4->T2  T4->T2  T4->T1  T4->T3  T5->T4  T5->T4  T5->T4  T5->T4  T5->T4  B2->T5  B2->T5  B2->T5  T6->B2  T6->B2  T6->B2  T6->T4  T6->T5  T2->T6  T2->T6  T2->T6  T2->T6  T2->T6  T2->T5  B1->T2  B1->T2  B1->T2  B2->T2  B2->T2  B2->T2`

## L160  (OK) — optimal 49, par 54

`T6->B1  T4->T6  T4->B1  T4->B1  T4->B2  T4->B2  T3->T4  T3->B2  B1->T4  B1->T4  B1->T4  T6->B1  T6->B1  T1->T6  T3->T1  T5->T3  T5->T3  T2->T5  T2->T5  T2->T4  T2->B1  T2->T3  B1->T2  B1->T2  B1->T2  T6->B1  T6->B1  T6->T2  T6->T2  T5->T6  T5->T6  T5->T6  B1->T6  B1->T6  T1->B1  T1->B1  T5->T1  T3->T5  T3->T5  T3->T5  T3->T5  T3->B1  T3->T1  B1->T3  B1->T3  B1->T3  B2->T3  B2->T3  B2->T3`

## L161  (OK) — optimal 60, par 65

`T1->B1  T5->B1  T5->B2  T6->B2  T3->T5  T3->T6  T3->T1  T3->B1  T3->B2  T3->T5  B2->T3  B2->T3  B2->T3  T6->B2  T6->B2  T6->T3  B1->T6  B1->T6  B1->T6  T1->B1  T1->B1  T1->T3  T1->B2  T5->T1  T5->T1  T5->T1  T2->T5  T2->T5  T4->T2  T4->T3  T4->T1  B1->T4  B1->T4  T5->B1  T5->B1  T5->B1  T5->T2  T5->T4  T6->T5  T6->T5  T6->T5  T6->T5  T6->T5  T2->T6  T2->T6  T2->T6  T2->T5  T2->T6  T1->T2  T1->T2  T1->T2  T1->T2  T1->T2  T1->T6  B1->T1  B1->T1  B1->T1  B2->T1  B2->T1  B2->T1`

## L162  (OK) — optimal 62, par 67

`T4->B1  T4->B2  T6->B2  T5->T4  T5->B2  T6->T5  T3->T6  B1->T6  T3->B1  T3->B1  T3->T4  T3->B1  T4->T3  T4->T3  T4->T3  T2->T5  T2->T4  T2->T4  T2->T3  T1->T2  B2->T2  B2->T2  B2->T2  T1->B2  T1->T3  T1->B2  T5->T1  T5->T1  T5->T1  T5->T1  T4->T5  T4->T5  T4->T5  T4->T5  T4->B2  T1->T4  T1->T4  T1->T4  T1->T4  T1->T4  T6->T1  T6->T1  T6->T1  T6->T1  T6->T4  T5->T6  T5->T6  T5->T6  T5->T6  T5->T6  T2->T5  T2->T5  T2->T5  T2->T5  T2->T5  T2->T1  B1->T2  B1->T2  B1->T2  B2->T2  B2->T2  B2->T2`

## L163  (OK) — optimal 65, par 70

`T1->B1  T1->B1  T4->B2  T5->T4  T5->B1  T2->T5  T2->B2  T3->T1  T3->T5  T3->T2  T3->B2  B1->T3  B1->T3  B1->T3  T5->B1  T5->B1  T5->B1  T5->T2  T5->T3  T6->T5  B1->T5  B1->T5  B1->T5  T2->B1  T2->B1  T2->B1  T2->T6  T2->T1  T2->T5  T3->T2  T3->T2  T3->T2  T3->T2  T3->T2  T1->T3  T1->T3  T1->T3  T4->T1  T4->T1  T4->T2  B2->T4  B2->T4  B2->T4  T1->B2  T1->B2  T1->B2  T1->T3  T6->T1  T6->T1  B2->T1  B2->T1  B2->T1  T6->B2  T6->B2  T6->T3  T4->T6  T4->T6  T4->T6  T4->T6  T4->T6  B1->T4  B1->T4  B1->T4  B2->T4  B2->T4`

## L164  (OK) — optimal 60, par 65

`T1->B1  T1->B2  T2->B1  T2->B1  T4->B2  T4->T1  T4->B2  T5->T4  T2->T5  T4->T2  T4->T2  B1->T4  B1->T4  B1->T4  T5->B1  T5->B1  T5->T4  B2->T5  B2->T5  B2->T5  T1->B2  T1->B2  T1->B1  T3->T2  T1->T3  T5->T1  T5->T1  T5->T1  T5->T1  T6->T1  B1->T5  B1->T5  B1->T5  T6->B1  T6->B2  T6->B1  T6->B1  T2->T6  T2->T6  T2->T6  T2->T6  T2->T6  B2->T2  B2->T2  B2->T2  T3->B2  T3->B2  T3->T2  T3->T2  T3->T5  T4->T3  T4->T3  T4->T3  T4->T3  T4->T3  B1->T4  B1->T4  B1->T4  B2->T4  B2->T4`

## L165  (OK) — optimal 55, par 60

`T4->B1  T5->B1  T5->B2  T1->T4  T3->T1  T5->T3  T1->T5  T1->T5  T1->B2  T1->B2  T1->T5  T4->T1  T4->T1  T4->T1  T6->T1  T6->B1  T2->T6  T2->T4  T2->T1  T4->T2  T4->T2  T4->T6  T4->T2  T5->T4  T5->T4  T5->T4  T5->T4  T5->T4  T2->T5  T2->T5  T2->T5  T2->T5  T2->T4  B1->T2  B1->T2  B1->T2  T6->B1  T6->B1  T6->B1  T6->T2  T6->T2  T6->T5  B2->T6  B2->T6  B2->T6  T3->B2  T3->B2  T3->T6  T3->T6  T3->T6  B1->T3  B1->T3  B1->T3  B2->T3  B2->T3`

## L166  (OK) — optimal 67, par 72

`T2->B1  T2->B2  T2->B2  T4->B2  T4->B1  T3->T4  T3->T2  T6->T3  T6->B1  T1->T4  T6->T1  T3->T6  T3->T6  T4->T3  T4->T3  T4->T3  T2->T4  T2->T4  T2->T4  T2->T6  T3->T2  T3->T2  T3->T2  T3->T2  T5->T2  T5->T3  T5->T3  T5->T2  B1->T5  B1->T5  B1->T5  T4->B1  T4->B1  T4->B1  T1->T3  T1->T3  T4->T1  T4->T5  T3->T4  T3->T4  T3->T4  T3->T4  T3->T4  T5->T3  T5->T3  T5->T3  T5->T3  T5->T3  B2->T5  B2->T5  B2->T5  T1->B2  T1->B2  T1->T5  T1->B2  T6->T1  T6->T1  T6->T1  T6->T1  T6->T1  T6->T5  B1->T6  B1->T6  B1->T6  B2->T6  B2->T6  B2->T6`

## L167  (OK) — optimal 64, par 69

`T1->B1  T6->B1  T3->T1  T3->B2  T1->T3  T1->T3  T5->T6  T1->T5  T1->B2  T1->B1  T1->B2  B1->T1  B1->T1  B1->T1  T6->B1  T6->B1  T6->T1  T6->B1  T3->T6  T3->T6  T3->T6  T3->T1  B2->T3  B2->T3  B2->T3  T4->B2  T4->B2  T4->T1  T4->B2  T4->T6  B1->T4  B1->T4  B1->T4  T5->B1  T5->B1  T5->T3  T5->T4  T5->B1  T5->T4  T6->T5  T6->T5  T6->T5  T6->T5  T6->T5  T3->T6  T3->T6  T3->T6  T3->T6  T3->T6  B2->T3  B2->T3  B2->T3  T2->B2  T2->B2  T2->T5  T2->B2  T2->T3  T2->T3  B1->T2  B1->T2  B1->T2  B2->T2  B2->T2  B2->T2`

## L168  (OK) — optimal 69, par 74

`T4->B1  T4->B1  T6->B2  T4->T6  B1->T4  B1->T4  T1->B1  T1->B2  T2->B1  T2->B2  T6->T1  T6->T1  T3->T6  T3->T4  T3->B1  T2->T6  T1->T2  T1->T2  T1->T2  T1->T3  T6->T1  T6->T1  T6->T1  T6->T3  T4->T6  T4->T6  T4->T6  T4->T6  T4->T3  T4->T1  T3->T4  T3->T4  T3->T4  T3->T4  T3->T4  T6->T3  T6->T3  T6->T3  T6->T3  T6->T3  T5->T6  B2->T6  B2->T6  B2->T6  T2->B2  T2->B2  T2->B2  T2->T5  T2->T4  T1->T2  T1->T2  T1->T2  T1->T2  T1->T2  B1->T1  B1->T1  B1->T1  T5->B1  T5->B1  T5->T1  T5->T1  T5->B1  T5->T6  B1->T5  B1->T5  B1->T5  B2->T5  B2->T5  B2->T5`

## L169  (OK) — optimal 65, par 70

`T1->B1  T3->B2  T4->B1  T6->B2  T2->T3  T2->T4  T2->T6  T5->T2  T5->B2  T5->T2  T5->B1  T2->T5  T2->T5  T2->T5  B2->T2  B2->T2  B2->T2  T1->B2  T1->B2  T4->T1  T4->T1  T4->T2  T4->T5  T3->T4  T3->T4  T3->B2  T3->T4  T1->T3  T1->T3  T1->T3  T1->T3  T1->T4  T2->T1  T2->T1  T2->T1  T2->T1  T2->T1  T6->T2  T6->T2  T6->T1  T2->T6  T2->T6  T2->T6  T4->T2  T4->T2  T4->T2  T4->T2  T4->T2  T6->T4  T6->T4  T6->T4  T6->T4  T6->T2  T3->T6  T3->T6  T3->T6  T3->T6  T3->T6  T3->T4  B1->T3  B1->T3  B1->T3  B2->T3  B2->T3  B2->T3`

## L170  (OK) — optimal 49, par 54

`T2->B1  T3->B2  T6->B1  T6->B1  T6->T2  T1->T6  T1->B2  T4->T6  T1->T3  T1->B2  T5->T4  T1->T5  T3->T1  T3->T1  T3->T1  T3->T1  T3->T6  B2->T3  B2->T3  B2->T3  T5->B2  T5->B2  T5->T3  T5->T1  B2->T5  B2->T5  T4->B2  T4->B2  T2->T4  T2->T4  T2->T5  T2->B2  T4->T2  T4->T2  T4->T2  T4->T2  T4->T3  T2->T4  T2->T4  T2->T4  T2->T4  T2->T4  T2->T5  B1->T2  B1->T2  B1->T2  B2->T2  B2->T2  B2->T2`

## L171  (OK) — optimal 56, par 61

`T1->B1  T2->B1  T4->B2  T4->B2  T6->B2  T4->T1  T2->T4  T2->B1  T6->T2  T3->T6  T3->T2  T1->T3  T1->T3  T1->T2  T1->T4  T1->T6  T3->T1  T3->T1  T3->T1  T3->T1  T3->T4  T3->T1  T5->T3  B2->T3  B2->T3  B2->T3  T6->B2  T6->B2  T6->B2  T6->T5  T6->T3  T2->T6  T2->T6  T2->T6  T2->T6  T2->T6  T5->T2  T5->T2  B1->T2  B1->T2  B1->T2  T5->B1  T5->B1  T5->B1  T4->T5  T4->T5  T4->T5  T4->T5  T4->T3  T4->T5  B1->T4  B1->T4  B1->T4  B2->T4  B2->T4  B2->T4`

## L172  (OK) — optimal 65, par 70

`T1->B1  T2->B1  T2->B1  T5->B2  T1->T2  T1->T5  T2->T1  T2->T1  T2->B2  T2->B2  B1->T2  B1->T2  B1->T2  T6->B1  T3->T6  T3->B1  T3->T1  T3->B1  T3->T2  T1->T3  T1->T3  T1->T3  T1->T3  B1->T1  B1->T1  B1->T1  T6->B1  T6->B1  T6->T1  B2->T6  B2->T6  B2->T6  T4->B2  T4->B1  T4->B2  T4->T3  T4->B2  T4->T2  T1->T4  T1->T4  T1->T4  T1->T4  T1->T4  T5->T1  T5->T1  B1->T1  B1->T1  B1->T1  T5->B1  T5->B1  T6->T5  T6->T5  T6->T5  T6->T5  T6->T4  T5->T6  T5->T6  T5->T6  T5->T6  T5->T6  B1->T5  B1->T5  B2->T5  B2->T5  B2->T5`

## L173  (OK) — optimal 64, par 69

`T2->B1  T5->B2  T5->B2  T5->B1  T6->B2  T4->T6  T3->T4  T5->T3  T5->B1  B2->T5  B2->T5  B2->T5  T1->B2  T2->B2  T3->T2  T3->T2  T3->T1  T3->B2  T3->T5  T1->T3  T1->T3  T6->T3  T6->T3  T6->T1  T6->T3  T6->T1  T6->T5  T2->T6  T2->T6  T2->T6  T4->T2  T4->T2  T4->T2  T4->T6  T2->T4  T2->T4  T2->T4  T2->T4  B2->T2  B2->T2  B2->T2  T1->B2  T1->B2  T1->B2  T1->T6  T1->T2  T1->T6  T2->T1  T2->T1  T2->T1  T2->T1  T2->T1  T4->T2  T4->T2  T4->T2  T4->T2  T4->T2  T4->T1  B1->T4  B1->T4  B1->T4  B2->T4  B2->T4  B2->T4`

## L174  (OK) — optimal 65, par 70

`T1->B1  T2->B1  T5->B1  T5->B2  T6->B2  T6->T2  T5->T6  T2->T5  T2->T5  T1->T2  T1->T5  T1->T6  T1->B2  B1->T1  B1->T1  B1->T1  T2->B1  T2->B1  T3->T2  T3->B1  T3->T1  B2->T3  B2->T3  B2->T3  T2->B2  T2->B2  T5->T2  T5->T2  T5->T2  T5->T2  T5->B2  T4->T5  T4->T1  B1->T5  B1->T5  B1->T5  T4->B1  T4->B1  T4->T5  T2->T4  T2->T4  T2->T4  T2->T4  T2->T4  T3->T2  T3->T2  T3->T2  T3->T2  T6->T3  T6->T3  T6->T3  T6->T3  T6->B1  T3->T6  T3->T6  T3->T6  T3->T6  T3->T6  T3->T2  B1->T3  B1->T3  B1->T3  B2->T3  B2->T3  B2->T3`

## L175  (OK) — optimal 60, par 65

`T1->B1  T2->B1  T4->B1  T6->B2  T4->T6  T1->T4  T2->T1  T5->T2  T5->B2  T5->T2  T5->T4  B1->T5  B1->T5  B1->T5  T6->B1  T6->B1  T6->T5  T6->B2  T6->T1  T3->T6  T3->B1  B2->T6  B2->T6  B2->T6  T3->B2  T3->T6  T3->B2  T2->T3  T2->T3  T2->T3  T2->B2  B1->T2  B1->T2  B1->T2  T4->B1  T4->B1  T4->B1  T4->T3  T4->T2  T4->T3  T2->T4  T2->T4  T2->T4  T2->T4  T2->T4  B1->T2  B1->T2  B1->T2  T1->B1  T1->B1  T1->B1  T1->T2  T1->T4  T1->T2  B1->T1  B1->T1  B1->T1  B2->T1  B2->T1  B2->T1`

## L176  (OK) — optimal 60, par 65

`T4->B1  T4->B2  T4->B2  T5->B2  T1->T4  T5->T4  T5->T1  T5->B1  T5->B1  T4->T5  T4->T5  T4->T5  B2->T4  B2->T4  B2->T4  T6->B2  T3->T6  T3->B2  T1->T3  T1->T3  T1->T5  T2->T1  T2->T5  T1->T2  T1->T2  T3->T1  T3->T1  T3->T1  T3->T4  T3->B2  T3->T1  T4->T3  T4->T3  T4->T3  T4->T3  T4->T3  T2->T4  T2->T4  T2->T4  T2->T4  B2->T2  B2->T2  B2->T2  T6->B2  T6->B2  T6->T3  T6->B2  T6->T2  T1->T6  T1->T6  T1->T6  T1->T6  T1->T6  T1->T4  B1->T1  B1->T1  B1->T1  B2->T1  B2->T1  B2->T1`

## L177  (OK) — optimal 60, par 65

`T6->B1  T6->B1  T6->B2  T4->T6  T4->B2  T5->T4  T5->B1  T5->B2  T5->T4  T5->T6  B2->T5  B2->T5  B2->T5  T3->B2  T3->B2  T3->T5  T3->T5  T2->T6  T3->T2  T6->T3  T6->T3  T6->T3  T6->T3  T2->T6  T2->T6  T2->T3  T4->T2  T4->T2  T4->T2  T4->T6  B2->T4  B2->T4  T1->B2  T1->T4  T1->B2  T1->T6  T1->T4  T1->B2  T4->T1  T4->T1  T4->T1  T4->T1  T4->T1  T2->T4  T2->T4  T2->T4  T2->T4  T2->T1  T6->T2  T6->T2  T6->T2  T6->T2  T6->T2  T6->T4  B1->T6  B1->T6  B1->T6  B2->T6  B2->T6  B2->T6`

## L178  (OK) — optimal 75, par 80

`T3->B1  T5->B2  T6->B2  T6->B1  T2->T6  T2->B2  T3->T2  T3->T5  B1->T3  B1->T3  T4->B1  T4->T3  T6->T4  T6->T4  T2->T6  T2->T6  T2->B1  T2->T6  T2->B1  T3->T2  T3->T2  T3->T2  T3->T2  B2->T3  B2->T3  B2->T3  T1->B2  T5->B2  T5->B2  T5->T2  B1->T5  B1->T5  B1->T5  T3->B1  T3->B1  T3->B1  T3->T1  T3->T2  T1->T3  T1->T3  B1->T3  B1->T3  B1->T3  T1->B1  T4->T1  T4->T1  T4->T1  T4->B1  T4->B1  T6->T4  T6->T4  T6->T4  T6->T4  T6->T3  T1->T6  T1->T6  T1->T6  T1->T6  T5->T1  T5->T1  T5->T1  T5->T1  T5->T4  T1->T5  T1->T5  T1->T5  T1->T5  T1->T5  T1->T6  B1->T1  B1->T1  B1->T1  B2->T1  B2->T1  B2->T1`

## L179  (OK) — optimal 72, par 77

`T1->B1  T2->B2  T2->B2  T4->B1  T2->T4  T1->T2  T1->T2  T1->B1  T1->B2  T6->T1  B1->T1  B1->T1  B1->T1  T5->B1  T3->T6  T3->T5  T6->T3  T6->T3  T6->B1  T6->B1  B2->T6  B2->T6  B2->T6  T5->B2  T5->B2  T5->B2  T5->T1  T2->T5  T2->T5  T2->T5  T2->T6  T2->T5  T6->T2  T6->T2  T6->T2  T6->T2  T6->T2  T3->T6  T3->T6  T3->T6  T4->T3  T4->T3  T4->T6  B2->T4  B2->T4  B2->T4  T3->B2  T3->B2  T3->B2  T4->T3  T4->T3  T4->T3  T4->T3  T4->T2  T4->T6  T3->T4  T3->T4  T3->T4  T3->T4  T3->T4  T5->T3  T5->T3  T5->T3  T5->T3  T5->T3  T5->T4  B1->T5  B1->T5  B1->T5  B2->T5  B2->T5  B2->T5`

## L180  (OK) — optimal 56, par 61

`T2->B1  T3->B2  T3->B2  T4->B1  T4->B2  T2->T4  T2->B1  T6->T2  T6->T2  T5->T6  T5->T4  T5->T2  T5->T3  T2->T5  T2->T5  T2->T5  T2->T5  T2->T6  T2->T3  T5->T2  T5->T2  T5->T2  T5->T2  T5->T2  T3->T5  T3->T5  T3->T5  T3->T5  T3->T2  T3->T5  B1->T3  B1->T3  B1->T3  T1->B1  T1->B1  T1->T3  T1->T3  T4->T1  T4->T1  T4->T1  T4->B1  B2->T4  B2->T4  B2->T4  T6->B2  T6->B2  T6->B2  T6->T1  T6->T3  T6->T4  B1->T6  B1->T6  B1->T6  B2->T6  B2->T6  B2->T6`

## L181  (OK) — optimal 57, par 62

`T2->B1  T2->B2  T5->B2  T5->B1  T6->B1  T1->T5  T1->T2  T6->T5  T1->T6  B1->T1  B1->T1  B1->T1  T4->B1  T4->B1  T4->B1  T4->B2  T4->T6  B2->T4  B2->T4  B2->T4  T3->B2  T3->T4  T3->T4  T2->T3  T2->T3  T2->T3  T2->B2  T6->T2  T6->T2  T6->T2  T5->T6  T5->T6  T5->T6  T5->B2  T6->T5  T6->T5  T6->T5  T6->T5  T6->T2  T6->T2  T3->T6  T3->T6  T3->T6  T3->T6  T3->T6  T1->T3  T1->T3  T1->T3  T1->T3  T1->T6  T1->T3  B1->T1  B1->T1  B1->T1  B2->T1  B2->T1  B2->T1`

## L182  (OK) — optimal 56, par 61

`T1->B1  T3->B1  T3->B2  T3->B2  T4->B1  T5->B2  T2->T3  T4->T5  T4->T2  T1->T4  T1->T3  T4->T1  T4->T1  T4->T3  T2->T4  T2->T4  T6->T4  T5->T2  T5->T2  T6->T1  T5->T6  T5->T6  T5->T4  T3->T5  T3->T5  T3->T5  T3->T5  B1->T3  B1->T3  B1->T3  T6->B1  T6->B1  T6->B1  T6->T4  T2->T6  T2->T6  T2->T6  T2->T6  T2->T3  T1->T2  T1->T2  T1->T2  T1->T2  T1->T5  T6->T1  T6->T1  T6->T1  T6->T1  T6->T1  T6->T2  B1->T6  B1->T6  B1->T6  B2->T6  B2->T6  B2->T6`

## L183  (OK) — optimal 61, par 66

`T1->B1  T3->B2  T3->B2  T4->B1  T5->T1  T5->B1  T5->B2  T1->T5  T1->T5  T4->T1  T3->T4  T2->T3  T2->T1  T2->T3  T2->T4  T2->T3  T2->T5  T4->T2  T4->T2  T4->T2  B1->T4  B1->T4  B1->T4  T1->B1  T1->B1  T1->B1  T1->T2  T1->T2  B2->T1  B2->T1  B2->T1  T4->B2  T6->B2  T6->B2  T6->T1  B1->T6  B1->T6  B1->T6  T4->B1  T4->B1  T4->B1  T3->T4  T3->T4  T3->T4  T3->T4  T6->T3  T6->T3  T6->T3  T6->T3  T5->T6  T5->T6  T5->T6  T5->T6  T5->T2  T5->T1  B1->T5  B1->T5  B1->T5  B2->T5  B2->T5  B2->T5`

## L184  (OK) — optimal 62, par 67

`T5->B1  T5->B2  T5->B1  T6->B2  T6->B2  T6->T5  T6->B1  B2->T6  B2->T6  B2->T6  T2->B2  T4->B2  T4->T2  T1->T4  T1->T5  T2->T1  T2->T1  T2->T6  T2->B2  T2->T5  T2->T4  T1->T2  T1->T2  T1->T2  T4->T1  T4->T1  T4->T1  T4->T2  B1->T4  B1->T4  B1->T4  T3->B1  T3->T2  T3->B1  T3->T4  T3->B1  T1->T3  T1->T3  T1->T3  T1->T3  T5->T1  T5->T1  T5->T1  T5->T1  T5->T3  T6->T5  T6->T5  T6->T5  T6->T5  T6->T5  T4->T6  T4->T6  T4->T6  T4->T6  T4->T6  T4->T2  B1->T4  B1->T4  B1->T4  B2->T4  B2->T4  B2->T4`

## L185  (OK) — optimal 66, par 71

`T4->B1  T6->B2  T6->B2  T6->T4  T6->B1  T3->T6  T3->B1  T3->B2  T1->T6  T5->T6  T3->T5  B1->T3  B1->T3  B1->T3  T1->B1  T4->T1  T4->T1  T4->T3  T5->T4  T5->T4  T5->B1  T5->T4  T1->T5  T1->T5  T1->T5  T1->T5  T1->T6  T1->B1  T2->T1  T6->T1  T6->T1  T6->T1  T6->T1  T6->T1  T5->T6  T5->T6  T5->T6  T5->T6  T5->T6  T4->T5  T4->T5  T4->T5  T4->T5  B1->T4  B1->T4  B1->T4  T2->B1  T2->T5  T2->B1  T2->B1  T4->T2  T4->T2  T4->T2  T4->T2  T3->T4  T3->T4  T3->T4  T3->T4  T3->T4  T3->T2  B1->T3  B1->T3  B1->T3  B2->T3  B2->T3  B2->T3`

## L186  (OK) — optimal 58, par 63

`T1->B1  T3->B2  T5->B2  T3->T1  T2->T3  T2->B1  T2->B2  T5->T3  T2->T5  T2->B1  T3->T2  T3->T2  T3->T2  T3->T5  T6->T3  T4->T6  T4->T2  T1->T3  T1->T3  T1->T3  T1->T4  T1->T2  T1->T4  T6->T1  T6->T1  B1->T1  B1->T1  B1->T1  T4->B1  T4->B1  T4->B1  T4->T1  T4->T6  B2->T4  B2->T4  B2->T4  T5->B2  T6->B2  T6->B2  T6->T4  T6->T4  B1->T6  B1->T6  B1->T6  T5->B1  T5->B1  T5->T6  T5->T6  T3->T5  T3->T5  T3->T5  T3->T5  T3->T5  B1->T3  B1->T3  B2->T3  B2->T3  B2->T3`

## L187  (OK) — optimal 63, par 68

`T2->B1  T3->B2  T4->B2  T4->B1  T6->B1  T4->T3  T4->T2  T5->T4  T1->T5  B2->T4  B2->T4  T1->B2  T1->B2  T1->T6  T3->T1  T3->T1  T5->T3  T5->T3  T5->T4  T6->T5  T6->T5  T6->T1  T6->T5  T6->T1  T6->B2  T4->T6  T4->T6  T4->T6  T4->T6  T4->T6  T1->T4  T1->T4  T1->T4  T1->T4  T1->T4  B2->T1  B2->T1  B2->T1  T3->B2  T3->B2  T3->B2  T3->T6  T3->T1  B1->T3  B1->T3  B1->T3  T2->B1  T2->B1  T2->T3  T2->B1  T2->T3  T5->T2  T5->T2  T5->T2  T5->T2  T5->T2  T5->T1  B1->T5  B1->T5  B1->T5  B2->T5  B2->T5  B2->T5`

## L188  (OK) — optimal 69, par 74

`T1->B1  T3->B2  T3->B1  B2->T3  T2->B2  T2->B2  T5->T3  T5->B1  T1->T5  T1->B2  T6->T2  T1->T6  T2->T1  T2->T1  T2->T1  T2->T5  B2->T2  B2->T2  B2->T2  T5->B2  T5->B2  T5->B2  T4->T1  T4->T2  T5->T4  B1->T5  B1->T5  B1->T5  T6->B1  T6->B1  T4->T6  T4->T6  T4->T5  T3->T4  T3->T4  T3->T4  T3->T4  T3->T2  T3->B1  T4->T3  T4->T3  T4->T3  T4->T3  T4->T3  T1->T4  T1->T4  T1->T4  T1->T4  T1->T4  B1->T1  B1->T1  B1->T1  T6->B1  T6->B1  T6->B1  T6->T3  T6->T1  T5->T6  T5->T6  T5->T6  T5->T6  T5->T6  T5->T1  B1->T5  B1->T5  B1->T5  B2->T5  B2->T5  B2->T5`

## L189  (OK) — optimal 65, par 70

`T1->B1  T1->B2  T2->B2  T4->B1  T2->T1  T6->T2  T6->T1  T6->T4  T6->T2  T4->T6  T4->T6  T5->T4  T5->B1  T5->T6  T5->T6  T5->T4  T5->B2  T2->T5  T2->T5  T2->T5  T4->T5  T4->T5  T4->T5  T1->T4  T1->T4  T1->T4  T1->T2  B1->T1  B1->T1  B1->T1  T3->B1  T4->B1  T4->B1  T3->T1  T2->T3  T2->T3  B2->T2  B2->T2  B2->T2  T4->B2  T4->B2  T4->B2  T4->T2  T1->T4  T1->T4  T1->T4  T1->T4  T1->T4  T6->T1  T6->T1  T6->T1  T6->T1  T6->T1  T3->T6  T3->T6  T3->T6  T3->T4  T3->T6  T3->T6  B1->T3  B1->T3  B1->T3  B2->T3  B2->T3  B2->T3`

## L190  (OK) — optimal 48, par 53

`T1->B1  T4->B2  T4->B2  T1->T4  T1->B2  T1->B1  T3->T1  T3->T4  T1->T3  T1->T3  T1->B1  T2->T1  T3->T1  T3->T1  T3->T1  T5->T2  T3->T5  T3->T1  T2->T3  T2->T3  T2->T3  T2->T3  T2->T1  T2->T3  T4->T2  T4->T2  T4->T2  T4->T2  T4->T2  B2->T4  B2->T4  B2->T4  T5->B2  T5->B2  T5->T4  T5->B2  T5->T2  T6->T5  T6->T5  B1->T5  B1->T5  B1->T5  T6->B1  T6->T4  B1->T6  B2->T6  B2->T6  B2->T6`

## L191  (OK) — optimal 55, par 60

`T1->B1  T2->B2  T6->B2  T5->T6  T5->B2  T3->T5  T2->T3  T6->T2  T6->T2  T6->B1  T1->T6  T1->B1  T1->T5  B2->T1  B2->T1  B2->T1  T6->B2  T6->B2  T3->T6  T3->T6  T3->T1  T4->T6  B2->T3  B2->T3  T4->B2  T4->T3  T4->T6  T5->T4  T5->T4  T5->T4  T5->B2  T5->B2  T3->T5  T3->T5  T3->T5  T3->T5  T3->T4  T3->T5  T4->T3  T4->T3  T4->T3  T4->T3  T4->T3  T2->T4  T2->T4  T2->T4  T2->T4  T2->T3  T2->T4  B1->T2  B1->T2  B1->T2  B2->T2  B2->T2  B2->T2`

## L192  (OK) — optimal 52, par 57

`T3->B1  T5->B1  T6->B2  T5->T6  T5->B2  T2->T3  T2->T5  T6->T5  T6->T5  T6->T2  T4->T6  T4->B2  T4->T6  T4->T6  T4->T2  T4->B1  B2->T4  B2->T4  B2->T4  T3->B2  T3->B2  T3->T4  T3->T4  T6->T3  T6->T3  T6->T3  T6->T3  T6->B2  T5->T6  T5->T6  T5->T6  T5->T6  T2->T5  T2->T5  T2->T5  T2->T4  T2->T6  B1->T2  B1->T2  B1->T2  T1->B1  T1->B1  T1->B1  T1->T5  T1->T2  T1->T2  B1->T1  B1->T1  B1->T1  B2->T1  B2->T1  B2->T1`

## L193  (OK) — optimal 65, par 70

`T2->B1  T2->B2  T3->B1  T6->B2  T1->T3  T1->B2  T1->B1  T1->T2  T3->T1  T3->T1  T3->T1  T4->T3  T4->T1  T4->T3  T5->T4  T5->T4  T6->T5  T6->T4  T6->T2  T5->T6  T5->T6  T2->T5  T2->T5  T2->T5  T2->T6  B2->T2  B2->T2  B2->T2  T3->B2  T3->B2  T3->B2  T3->T6  T3->T2  T1->T3  T1->T3  T1->T3  T1->T3  T1->T3  T6->T1  T6->T1  T6->T1  T6->T1  T6->T1  T4->T6  T4->T6  T4->T6  T4->T6  T4->T3  T2->T4  T2->T4  T2->T4  T2->T4  T2->T4  T5->T2  T5->T2  T5->T2  T5->T2  T5->T2  T5->T6  B1->T5  B1->T5  B1->T5  B2->T5  B2->T5  B2->T5`

## L194  (OK) — optimal 64, par 69

`T2->B1  T4->B2  T4->B1  T6->B2  T4->T6  T1->T4  T1->B1  T2->T1  T5->T2  T5->T4  T6->T5  T6->T5  T6->T1  T6->T4  T1->T6  T1->T6  T1->T6  T1->T6  T1->T2  T1->B2  T6->T1  T6->T1  T6->T1  T6->T1  T6->T1  T4->T6  T4->T6  T4->T6  T4->T6  T4->T1  T3->T4  T5->T4  T5->T4  T5->T4  T5->T3  B2->T5  B2->T5  B2->T5  T3->B2  T3->B2  T3->T4  T2->T3  T2->T3  T2->T3  T2->T5  T2->B2  T2->T6  T3->T2  T3->T2  T3->T2  T3->T2  T3->T2  T5->T3  T5->T3  T5->T3  T5->T3  T5->T3  T5->T2  B1->T5  B1->T5  B1->T5  B2->T5  B2->T5  B2->T5`

## L195  (OK) — optimal 56, par 61

`T1->B1  T6->B2  T1->T6  T1->B2  T1->B2  T1->B1  T4->T1  T5->T1  T6->T1  T6->T1  T3->T6  T3->B1  T4->T6  T5->T3  T4->T5  T4->T3  T4->T5  T4->T1  T2->T4  T2->T4  T3->T4  T3->T4  T3->T4  T3->T2  B1->T3  B1->T3  B1->T3  T2->B1  T2->B1  T2->B1  T2->T3  T6->T2  T6->T2  T6->T2  T6->T4  T6->T2  T3->T6  T3->T6  T3->T6  T3->T6  T3->T6  B1->T3  B1->T3  B1->T3  T5->B1  T5->B1  T5->B1  T5->T3  T5->T2  T5->T3  B1->T5  B1->T5  B1->T5  B2->T5  B2->T5  B2->T5`

## L196  (OK) — optimal 56, par 61

`T1->B1  T2->B1  T3->B2  T3->B2  T5->B2  T5->T3  T5->B1  T4->T3  T4->T2  T5->T4  T3->T5  T3->T5  T3->T5  T4->T3  T4->T3  T4->T3  T1->T4  T1->T5  T4->T1  T4->T1  T6->T1  T4->T6  T3->T4  T3->T4  T3->T4  T3->T4  T3->T4  T2->T3  T2->T3  B2->T3  B2->T3  B2->T3  T2->B2  T2->T4  T6->T2  T6->T2  T6->B2  T6->T2  T6->T2  T1->T6  T1->T6  T1->T6  T1->T6  T1->B2  T5->T1  T5->T1  T5->T1  T5->T1  T5->T1  T5->T6  B1->T5  B1->T5  B1->T5  B2->T5  B2->T5  B2->T5`

## L197  (OK) — optimal 61, par 66

`T2->B1  T2->B1  T5->B1  T5->B2  T6->B2  T6->T5  T6->B2  T2->T5  T3->T2  T6->T2  B1->T6  B1->T6  B1->T6  T3->B1  T3->T6  T3->T2  T4->T3  T4->T3  T4->T3  B1->T3  T4->B1  T4->B1  T4->B1  B2->T4  B2->T4  B2->T4  T1->B2  T1->B2  T1->T4  T1->T4  T1->B2  T6->T1  T6->T1  T6->T1  T6->T1  T6->T1  B1->T6  B1->T6  B1->T6  T5->B1  T5->B1  T5->B1  T5->T6  T5->T6  T2->T5  T2->T5  T2->T5  T2->T5  T2->T4  T3->T2  T3->T2  T3->T2  T3->T2  T3->T2  T3->T5  B1->T3  B1->T3  B1->T3  B2->T3  B2->T3  B2->T3`

## L198  (OK) — optimal 72, par 77

`T5->B1  T6->T5  T6->B1  T2->T6  T2->B2  T2->B2  T2->T6  T2->B2  T2->B1  T1->T2  B2->T2  B2->T2  B2->T2  T1->B2  T3->T1  T3->B2  T4->T3  T4->B2  T1->T4  T1->T4  T3->T1  T3->T1  B2->T3  B2->T3  B2->T3  T6->B2  T6->B2  T6->B2  T6->T1  T3->T6  T3->T6  T3->T6  T3->T6  B2->T3  B2->T3  B2->T3  T1->B2  T5->B2  T5->B2  T5->T3  T5->T2  T5->T2  B1->T5  B1->T5  B1->T5  T1->B1  T1->B1  T1->B1  T1->T5  T1->T5  T3->T1  T3->T1  T3->T1  T3->T1  T3->T1  T6->T3  T6->T3  T6->T3  T6->T3  T6->T3  T4->T6  T4->T6  T4->T6  T4->T6  T4->T6  T4->T1  B1->T4  B1->T4  B1->T4  B2->T4  B2->T4  B2->T4`

## L199  (OK) — optimal 73, par 78

`T5->B1  T5->B2  T5->B2  T2->T5  T2->B1  T2->T5  T3->T5  T4->T3  T4->T2  T6->T4  T6->T4  T6->T2  T6->T2  T6->B1  T6->B2  T3->T6  T3->T6  T4->T6  T4->T6  T4->T6  T4->T3  T5->T4  T5->T4  T5->T4  T5->T4  B2->T5  B2->T5  B2->T5  T3->B2  T3->B2  T3->B2  B1->T3  B1->T3  B1->T3  T2->B1  T2->B1  T1->T5  T1->B1  T1->T3  B2->T1  B2->T1  B2->T1  T2->B2  T2->B2  T2->B2  T1->T2  T1->T2  T1->T2  T1->T2  T1->T2  T4->T1  T4->T1  T4->T1  T4->T1  T4->T1  T4->T6  T3->T4  T3->T4  T3->T4  T3->T4  T3->T4  T5->T3  T5->T3  T5->T3  T5->T3  T5->T3  T5->T4  B1->T5  B1->T5  B1->T5  B2->T5  B2->T5  B2->T5`

## L200  (OK) — optimal 68, par 73

`T2->B1  T2->B1  T2->B1  T2->B2  B1->T2  B1->T2  B1->T2  T6->B1  T1->T2  T1->B1  T6->T1  T6->B2  T5->T1  T6->T5  T6->B1  T6->B2  B1->T6  B1->T6  B1->T6  T5->B1  T5->B1  T5->T6  T5->T6  T4->T5  T4->T5  T4->T5  T4->B1  T4->T6  B1->T4  B1->T4  B1->T4  T2->B1  T2->B1  T2->B1  T3->T5  T3->T4  T2->T3  T2->T3  T2->T4  T5->T2  T5->T2  T5->T2  T5->T2  T5->T2  B2->T5  B2->T5  B2->T5  T3->B2  T3->B2  T3->B2  T3->T5  T1->T3  T1->T3  T1->T3  T1->T3  T1->T2  T3->T1  T3->T1  T3->T1  T3->T1  T3->T1  T3->T5  B1->T3  B1->T3  B1->T3  B2->T3  B2->T3  B2->T3`


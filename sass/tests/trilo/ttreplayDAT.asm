
	.db      0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0
	.db      0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0
	.db      0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0
	.db      0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0
	.db      0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0
	.db      0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0
	.db      0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0
	.db      0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0
_VOLUME_TABLE:
	.INCBIN ".\tests\trilo\voltable_combined.bin"	

;Konami values found in	Nemesis 2 replayer.
C_PER		.equ	$6a*32	
C1_PER		.equ	$64*32
D_PER		.equ	$5e*32
D1_PER		.equ	$59*32
E_PER		.equ	$54*32
F_PER		.equ	$4f*32
F1_PER		.equ	$4a*32
G_PER		.equ	$46*32
G1_PER		.equ	$42*32
A_PER		.equ	$3f*32
A1_PER		.equ	$3b*32
B_PER		.equ	$38*32

TRACK_ToneTable:	
	.dw C_PER/1	,C1_PER/1  ,D_PER/1  ,D1_PER/1  ,E_PER/1	,F_PER/1  ,F1_PER/1  ,G_PER/1	 ,G1_PER/1	,A_PER/1  ,A1_PER/1  ,B_PER/1
	.dw C_PER/2	,C1_PER/2  ,D_PER/2  ,D1_PER/2  ,E_PER/2	,F_PER/2  ,F1_PER/2  ,G_PER/2	 ,G1_PER/2	,A_PER/2  ,A1_PER/2  ,B_PER/2
	.dw C_PER/4	,C1_PER/4  ,D_PER/4  ,D1_PER/4  ,E_PER/4	,F_PER/4  ,F1_PER/4  ,G_PER/4	 ,G1_PER/4	,A_PER/4  ,A1_PER/4  ,B_PER/4
	.dw C_PER/8	,C1_PER/8  ,D_PER/8  ,D1_PER/8  ,E_PER/8	,F_PER/8  ,F1_PER/8  ,G_PER/8	 ,G1_PER/8	,A_PER/8  ,A1_PER/8  ,B_PER/8
	.dw C_PER/16	,C1_PER/16 ,D_PER/16 ,D1_PER/16 ,E_PER/16	,F_PER/16 ,F1_PER/16 ,G_PER/16 ,G1_PER/16	,A_PER/16 ,A1_PER/16 ,B_PER/16
	.dw C_PER/32	,C1_PER/32 ,D_PER/32 ,D1_PER/32 ,E_PER/32	,F_PER/32 ,F1_PER/32 ,G_PER/32 ,G1_PER/32	,A_PER/32 ,A1_PER/32 ,B_PER/32
	.dw C_PER/64	,C1_PER/64 ,D_PER/64 ,D1_PER/64 ,E_PER/64	,F_PER/64 ,F1_PER/64 ,G_PER/64 ,G1_PER/64	,A_PER/64 ,A1_PER/64 ,B_PER/64
	.dw C_PER/128,C1_PER/128,D_PER/128,D1_PER/128,E_PER/128,F_PER/128,F1_PER/128,G_PER/128,G1_PER/128,A_PER/128,A1_PER/128,B_PER/128


TRACK_Vibrato_sine:
	.db	 0*2,	2*2, 4*2, 7*2,11*2,16*2,22*2,28*2,35*2,43*2,51*2,59*2,68*2,77*2,87*2,96*2
	.db	96*2,86*2,77*2,68*2,59*2,51*2,43*2,35*2,28*2,22*2,16*2,11*2, 7*2,	4*2, 2*2, 0*2

TRACK_Vibrato_triangle:
	.db	 0, 6,12,18,24,30,36,42,48,54,60,66,72,78,84,90
	.db	96,90,84,78,72,66,60,54,48,42,36,30,24,18,12, 6

TRACK_Vibrato_pulse:
	.db	255,255,255,255,255,255,255,255,255,255,255,255,255,255,255,255
	.db      0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0	
	
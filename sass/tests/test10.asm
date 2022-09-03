	.rom 

	.start init
	;db "MegaROM",1ah
    .org 04000h
    .db "AB"             ; ID bytes
    .dw init           	; cartridge initialization
    .dw 0                ; statement handler (not used)
    .dw 0                ; device handler (not used)
    .dw 0                ; BASIC program in ROM (not used, especially not in page 1)
    .dw 0,0,0            ; reserved

init:
	ei
	halt
	di
	ret	
	ld      a,-1
	ld      [0c000h],a
	jp      init

pattern2:	.ds 1    


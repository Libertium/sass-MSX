/* Megarom:
Just be careful that size of empty space remaining in block(s) (8KB) is calculated correctly. 
Also note that enabling the megaROM in 8000-BFFFh area is something you have to do yourself, 
before using it (unless you want to use RAM there). Check for example some existing megaROMs 
to see how they determine what slot they're in. And other than for 4000-5FFFh area, megaROM 
blocks may not be initialized after a reset (depending on cartridge type). If you ignore this, 
you may get a MegaROM that works fine on emulators but not (reliable) on real hardware.*/

megarom_bank0	.equ		04000h
megarom_bank1	.equ		06000h
megarom_bank2	.equ		08000h
megarom_bank3	.equ		0A000h

		.bios				;Predefine las direcciones de las rutinas de la BIOS, incluyendo las especificadas en 
							; los estándares MSX, MSX2, MSX2+ y Turbo-R. Se emplean los nombres habituales en mayúsculas.

		;.page 1				;Equivale a .ORG X pero no se indica una dirección sino un número de página de la memoria. 
							;Así, .PAGE 0 equivale a .ORG 0000h,y .PAGE 1 equivale a .ORG 4000h, 
							;.PAGE 2 equivale a .ORG 8000h y .PAGE 3 equivale a .ORG 0C000h

		;.megarom KonamiSCC	;[mapeador] Define la cabecera y estructura para producir una megaROM. 
							;Por defecto se define también la subpágina 0 del mapeador, por
							;lo que no es necesario incluir ninguna instrucción ORG ni PAGE o SUBPAGE previa. 
							;Los tipos de mapeador soportado son los siguientes: 
							; Konami: subpagina de 8 KB, límite de 32 pag. Entre 4000h-5FFFh esta necesariamente la subpágina 0, no puede cambiarse.
							; KonamiSCC: subpágina de 8 KB, límite de 64 páginas. Limite de 512 KB (4 megabits). Soporta acceso a SCC.
							; ASCII8: tamaño de subpágina de 8 KB, límite de 256 pag. Maximo megaROM de 2048 KB (16 megabits, 2 megabytes).
							; ASCII16: subpágina de 16 KB, límite de 256 paginas. El tamaño máximo del megaROM sera 4096 KB (32 megabits).
		;.start init
		;db "MegaROM",1ah

        org 04000h
        db "AB"             ; ID bytes
        dw init            	; cartridge initialization
        dw 0                ; statement handler (not used)
        dw 0                ; device handler (not used)
        dw 0                ; BASIC program in ROM (not used, especially not in page 1)
        dw 0,0,0            ; reserved

main:
        ld a,1
        ld [06000h],a     ; switch ROM block 1 into 6000-7FFFh area (in case reset doesn't do this automatically)
        call screen2
loop:
        jr loop

        ;ds 06000h-$,0EEh    ; fill rest of 4000-5fffh area (EEh = easy to recognize in hex viewer)

        org 05000h
init:   call RSLREG			;bios.RSLREG
        jp main
        rrca
        rrca
        and 0x03
        ld c,a
        ld b,0
        ld hl, 0xFCC1 ;bios.EXPTBL
        add hl,bc
        or [hl]
        ld b,a
        inc hl
        inc hl
        inc hl
        inc hl
        ld a,[hl]
        and 0x0c
        or b
        ld h,0x80
        call ENASLT
        jp main

		RET
        RET
        RET
        RET

; What follows here, should wind up at offset 2000h in generated binary
; (check with hex viewer if that's the case)
; = block nr. 1 (2nd block) of MegaROM
; Assembler's program counter should be 6000h at this point
; (check "block1" label in assembler list output to see if that's the case)

		;.subpage 1 at $6000	
block1:
screen2:
        ld hl,0f3e9h                                   ; Color 15,0,0
        ld [hl],15
        inc hl
        ld [hl],0
        inc hl
        ld [hl],0
        call INIGRP                                    ; Screen 2,2
        ld bc,0e201h
        jp WRTVDP                                    ; CALL+RET shortened to JP

        ;ds 08000h-$,0EEh      ; fill rest of 6000-7FFFh area

; = block nr. 2 (3rd block) of MegaROM
; Assembler's program counter should be 8000h at this point
; etc, etc.



/*
block2:
		.page 2					; equivale a .ORG 8000h 
		.subpage 2 at $8000	
		ld	a,0
		ret

		.subpage 3 at $A000	
		ld	a,0
		ret
*/

/*
You seem to rely on assembler directives to produce the code. Maybe that's easier, but you don't need it,
 MSX cartridges are really simple from a programmer's perspective. It does however:

	Make sure that to other people reading your code, it may not be clear what's going on.
	Make it impossible to assemble your code using other assemblers. Which makes it difficult for others 
	to help, AND creates (unnecessary) problems should you ever decide to use another assembler.
	Something like this should be clear to any assembler, and easy to read for Z80 programmers:
	In other ROM blocks, you can use "org" assembler statements to set program counter at addresses 
	where you plan to access these blocks. Just be careful that size of empty space remaining in block(s) 
	is calculated correctly. Also note that enabling the megaROM in 8000-BFFFh area is something you 
	have to do yourself, before using it (unless you want to use RAM there). Check for example some 
	existing megaROMs to see how they determine what slot they're in. And other than for 4000-5FFFh area, 
	megaROM blocks may not be initialized after a reset (depending on cartridge type). If you ignore this, 
	you may get a MegaROM that works fine on emulators but not (reliable) on real hardware.
	Btw - didn't check what graphics stuff you're trying to do, I'm not much of an MSX graphics coder... 
*/

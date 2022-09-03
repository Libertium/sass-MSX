	.PAGE 2
.basic
.start inicio

inicio:
	ld		a,3
	ld		b,5
	LD		HL,dire1
	LD		de,dire2
	RET
dire1:
 .db 1,2,3,4,5,6,7,8,9,10

.page 3
dire2:
	.ds 30

